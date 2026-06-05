using System;
using System.Collections.Generic;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Manages player guilds — founding, membership, ranks, guild points, and tier upgrades.
///
/// Add as a component on a persistent Managers GameObject.
/// Assign a GuildConfig asset in the Inspector.
///
/// Events:
///   OnGuildFounded(Guild)
///   OnGuildTierUp(Guild, int newTier)
///   OnMemberJoined(Guild, GuildMember)
///   OnMemberLeft(Guild, string memberId)
///   OnGuildQuestPosted(Guild, QuestDefinition)
/// </summary>
public class GuildManager : MonoBehaviour
{
    public static GuildManager Instance { get; private set; }

    public static event Action<Guild>                    OnGuildFounded;
    public static event Action<Guild, int>               OnGuildTierUp;
    public static event Action<Guild, GuildMember>       OnMemberJoined;
    public static event Action<Guild, string>            OnMemberLeft;
    public static event Action<Guild, QuestDefinition>   OnGuildQuestPosted;

    // -------------------------------------------------------------------------
    // Data model
    // -------------------------------------------------------------------------

    [System.Serializable]
    public class GuildMember
    {
        public string memberId;
        public string displayName;
        public int    rank;           // 0=Recruit … 5=Guild Master
        public int    contributedGP;
        public System.DateTime joinDate;

        public string RankName => GuildManager.Instance != null && GuildManager.Instance.config != null
            ? GuildManager.Instance.config.rankNames[Mathf.Clamp(rank, 0, 5)]
            : rank.ToString();
    }

    [System.Serializable]
    public class Guild
    {
        public string guildId;
        public string guildName;
        public string emblemId;           // Resolved by UI — just an identifier
        public int    guildPoints;
        public int    tier;               // 0=Bronze … 4=Diamond
        public string masterId;

        public readonly List<GuildMember>   members        = new();
        public readonly List<string>        activeQuestIds = new();
        public readonly HashSet<string>     storageItemIds = new();  // Simplified shared storage

        public int MemberCount => members.Count;
        public GuildMember GetMember(string id) => members.Find(m => m.memberId == id);
    }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Configuration")]
    public GuildConfig config;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private readonly List<Guild> guilds = new();

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        // Hook external events for GP awards
        QuestManager.OnQuestCompleted  += HandleQuestCompleted;
        TownManager.OnBuildingPlaced   += (_, _) => AwardGPToPlayerGuild(config != null ? config.gpPerBuildingBuilt : 10);
    }

    // -------------------------------------------------------------------------
    // Public API — Guild lifecycle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Found a new guild. Returns null if requirements not met.
    /// founderId/founderName represent the founding player.
    /// </summary>
    public Guild FoundGuild(string guildName, string emblemId, string founderId, string founderName, PlayerInventory inventory)
    {
        if (config == null) { Debug.LogWarning("[GuildManager] No GuildConfig assigned."); return null; }

        // Level check
        if (XPSystem.Instance != null && XPSystem.Instance.CurrentLevel < config.minimumLevelToFound)
        {
            SystemNotifier.Instance?.Announce(
                $"You need to be Level {config.minimumLevelToFound} to found a guild.",
                SystemNotifier.MessageType.Warning);
            return null;
        }

        // Gold check (via CurrencySystem if available)
        if (CurrencySystem.Instance != null)
        {
            if (CurrencySystem.Instance.Gold < config.foundingGoldCost)
            {
                SystemNotifier.Instance?.Announce(
                    $"Founding a guild costs {config.foundingGoldCost} Gold.",
                    SystemNotifier.MessageType.Warning);
                return null;
            }
            CurrencySystem.Instance.SpendGold(config.foundingGoldCost);
        }

        // Already in a guild?
        if (FindMemberGuild(founderId) != null)
        {
            SystemNotifier.Instance?.Announce("You are already in a guild.", SystemNotifier.MessageType.Warning);
            return null;
        }

        var guild = new Guild
        {
            guildId   = $"guild_{guilds.Count}_{guildName.Replace(" ", "_").ToLower()}",
            guildName = guildName,
            emblemId  = emblemId,
            tier      = 0,
            masterId  = founderId,
        };

        var master = new GuildMember
        {
            memberId     = founderId,
            displayName  = founderName,
            rank         = 5,   // Guild Master
            joinDate     = System.DateTime.UtcNow,
        };
        guild.members.Add(master);
        guilds.Add(guild);

        SystemNotifier.Instance?.Announce($"Guild '{guildName}' has been established!", SystemNotifier.MessageType.WorldEvent);
        OnGuildFounded?.Invoke(guild);
        TitleSystem.Instance?.UnlockTitle("guild_master");       // Unlock title if it exists
        ActionTracker.Instance?.LogAction(founderId, "GuildFounded", guildName, 200);

        return guild;
    }

    /// <summary>Invite and add a member at Recruit rank. Returns false if guild is full or member is already in one.</summary>
    public bool AddMember(Guild guild, string memberId, string displayName)
    {
        if (guild == null) return false;
        if (FindMemberGuild(memberId) != null) return false;

        int cap = config != null ? config.memberCaps[Mathf.Clamp(guild.tier, 0, 4)] : 20;
        if (guild.MemberCount >= cap)
        {
            SystemNotifier.Instance?.Announce(
                $"{guild.guildName} is at maximum capacity ({cap} members).",
                SystemNotifier.MessageType.Warning);
            return false;
        }

        var member = new GuildMember
        {
            memberId    = memberId,
            displayName = displayName,
            rank        = 0,    // Recruit
            joinDate    = System.DateTime.UtcNow,
        };
        guild.members.Add(member);

        SystemNotifier.Instance?.Announce($"{displayName} has joined {guild.guildName}!", SystemNotifier.MessageType.Info);
        OnMemberJoined?.Invoke(guild, member);
        ActionTracker.Instance?.LogAction(memberId, "GuildJoined", guild.guildId, 50);
        return true;
    }

    /// <summary>Remove a member from their guild. Pass rankOfActing = rank of the player performing the kick.</summary>
    public bool RemoveMember(Guild guild, string targetId, int rankOfActing)
    {
        if (guild == null) return false;
        var target = guild.GetMember(targetId);
        if (target == null) return false;
        if (rankOfActing <= target.rank) return false;   // Can't kick equal or higher rank
        if (targetId == guild.masterId) return false;    // Can't remove GM

        guild.members.Remove(target);
        SystemNotifier.Instance?.Announce($"{target.displayName} has left {guild.guildName}.", SystemNotifier.MessageType.Info);
        OnMemberLeft?.Invoke(guild, targetId);
        return true;
    }

    /// <summary>Promote or demote a member (acting rank must be strictly higher than target's new rank).</summary>
    public bool SetMemberRank(Guild guild, string targetId, int newRank, int actingRank)
    {
        var target = guild?.GetMember(targetId);
        if (target == null) return false;
        if (actingRank <= newRank) return false;    // Must outrank the rank you're assigning

        target.rank = Mathf.Clamp(newRank, 0, 5);
        string rankName = config != null ? config.rankNames[target.rank] : newRank.ToString();
        SystemNotifier.Instance?.Announce(
            $"{target.displayName} is now {rankName} of {guild.guildName}.",
            SystemNotifier.MessageType.Info);
        return true;
    }

    /// <summary>Post a quest to the guild notice board.</summary>
    public bool PostGuildQuest(Guild guild, string questId, string actingMemberId)
    {
        if (guild == null) return false;
        var actor = guild.GetMember(actingMemberId);
        if (actor == null || actor.rank < 3) return false;  // Officer or higher

        if (!guild.activeQuestIds.Contains(questId))
            guild.activeQuestIds.Add(questId);

        // Pass null def — the event subscriber can look it up by questId if needed
        OnGuildQuestPosted?.Invoke(guild, (QuestDefinition)null);
        ActionTracker.Instance?.LogAction(actingMemberId, "GuildQuestPosted", questId, 15);
        return true;
    }

    /// <summary>Award Guild Points to the guild that the local player belongs to.</summary>
    public void AwardGPToPlayerGuild(int gp, string playerId = "local_player")
    {
        var guild = FindMemberGuild(playerId);
        if (guild == null) return;

        guild.guildPoints += gp;

        var member = guild.GetMember(playerId);
        if (member != null) member.contributedGP += gp;

        CheckTierUpgrade(guild);
    }

    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    public Guild FindMemberGuild(string memberId)
    {
        foreach (var g in guilds)
            foreach (var m in g.members)
                if (m.memberId == memberId) return g;
        return null;
    }

    public Guild FindGuildById(string id)
    {
        foreach (var g in guilds) if (g.guildId == id) return g;
        return null;
    }

    public IReadOnlyList<Guild> AllGuilds => guilds;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void CheckTierUpgrade(Guild guild)
    {
        if (config == null) return;
        for (int t = config.tierGPThresholds.Length - 1; t > guild.tier; t--)
        {
            if (guild.guildPoints >= config.tierGPThresholds[t])
            {
                guild.tier = t;
                string tierName = t < config.tierNames.Length ? config.tierNames[t] : t.ToString();
                SystemNotifier.Instance?.Announce(
                    $"Guild '{guild.guildName}' has risen to {tierName} tier!",
                    SystemNotifier.MessageType.WorldEvent);
                OnGuildTierUp?.Invoke(guild, t);
                break;
            }
        }
    }

    private void HandleQuestCompleted(QuestDefinition def)
    {
        AwardGPToPlayerGuild(config != null ? config.gpPerQuestComplete : 20);
    }
}
