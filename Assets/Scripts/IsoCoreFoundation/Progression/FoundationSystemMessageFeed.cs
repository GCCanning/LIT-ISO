using System;
using System.Collections.Generic;

namespace IsoCore.Foundation
{
    public sealed class SystemMessageFeed
    {
        readonly List<FoundationSystemMessageEntry> _messages = new();
        int _nextSequence;

        public event Action<FoundationSystemMessageEntry> Queued;

        public IReadOnlyList<FoundationSystemMessageEntry> Messages => _messages;

        public FoundationSystemMessageEntry Queue(SystemMessageChannel channel, string text, string sourceId = "", int priority = 1)
        {
            if (string.IsNullOrWhiteSpace(text)) return default;

            var entry = new FoundationSystemMessageEntry
            {
                sequence = ++_nextSequence,
                channel = channel,
                text = text.Trim(),
                sourceId = sourceId ?? "",
                priority = Math.Max(0, priority),
            };

            _messages.Insert(0, entry);
            const int maxMessages = 64;
            while (_messages.Count > maxMessages)
                _messages.RemoveAt(_messages.Count - 1);

            Queued?.Invoke(entry);
            return entry;
        }

        public FoundationSystemMessageEntry[] CaptureState() => _messages.ToArray();

        public void RestoreState(FoundationSystemMessageEntry[] messages)
        {
            _messages.Clear();
            _nextSequence = 0;
            if (messages == null) return;

            for (int i = 0; i < messages.Length; i++)
            {
                var entry = messages[i];
                if (string.IsNullOrWhiteSpace(entry.text)) continue;
                _messages.Add(entry);
                _nextSequence = Math.Max(_nextSequence, entry.sequence);
            }
        }
    }

    [Serializable]
    public struct FoundationSystemMessageEntry
    {
        public int sequence;
        public SystemMessageChannel channel;
        public string text;
        public string sourceId;
        public int priority;
    }
}
