using System;
using System.Globalization;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class IsoRuntimeRecorder : MonoBehaviour
{
    public static IsoRuntimeRecorder Instance { get; private set; }

    [Header("Recording")]
    public bool recordOnPlay = true;
    public float playerSampleInterval = 0.5f;
    public float duplicateBlockedMoveSuppressWindow = 0.25f;

    [Header("References")]
    public IsoWorldChunkManager world;
    public Transform player;

    private StreamWriter writer;
    private float nextPlayerSampleTime;
    private string recordingPath;
    private string lastBlockedMoveDetail;
    private float lastBlockedMoveTime = -999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateForIsoWorld()
    {
        if (FindFirstObjectByType<IsoRuntimeRecorder>() != null)
        {
            return;
        }

        IsoWorldChunkManager world = FindFirstObjectByType<IsoWorldChunkManager>();
        if (world == null)
        {
            return;
        }

        GameObject recorderObject = new GameObject("Iso Runtime Recorder");
        IsoRuntimeRecorder recorder = recorderObject.AddComponent<IsoRuntimeRecorder>();
        recorder.world = world;
        recorder.player = world.player;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (world == null)
        {
            world = FindFirstObjectByType<IsoWorldChunkManager>();
        }

        if (player == null && world != null)
        {
            player = world.player;
        }

        if (recordOnPlay)
        {
            BeginRecording();
        }
    }

    private void Update()
    {
        if (writer == null || Time.unscaledTime < nextPlayerSampleTime)
        {
            return;
        }

        nextPlayerSampleTime = Time.unscaledTime + Mathf.Max(0.1f, playerSampleInterval);
        RecordPlayerSample();
    }

    private void OnDestroy()
    {
        EndRecording();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        EndRecording();
    }

    public void BeginRecording()
    {
        if (writer != null)
        {
            return;
        }

        string directory = GetRecordingDirectory();
        Directory.CreateDirectory(directory);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        recordingPath = Path.Combine(directory, $"iso_session_{timestamp}.jsonl");
        writer = new StreamWriter(recordingPath, append: false);
        writer.AutoFlush = true;

        RecordEvent("session_start", $"path={recordingPath}");
        Debug.Log($"Iso runtime recording started: {recordingPath}");
    }

    public void EndRecording()
    {
        if (writer == null)
        {
            return;
        }

        RecordEvent("session_end", string.Empty);
        writer.Dispose();
        writer = null;
        Debug.Log($"Iso runtime recording saved: {recordingPath}");
    }

    public void RecordChunkLoaded(Vector2Int chunkCoord)
    {
        RecordEvent("chunk_loaded", $"chunk={FormatVector2Int(chunkCoord)}");
    }

    public void RecordChunkUnloaded(Vector2Int chunkCoord)
    {
        RecordEvent("chunk_unloaded", $"chunk={FormatVector2Int(chunkCoord)}");
    }

    public void RecordTileSelection(Vector3Int cell, string action)
    {
        if (world != null)
        {
            IsoWorldChunkManager.GroundCellSample sample = world.SampleGroundCell(cell);
            RecordEvent(
                action,
                $"cell={FormatVector3Int(sample.Cell)} height={sample.Height} transition={sample.IsTransitionCell} edge={sample.IsHeightEdge}");
            return;
        }

        RecordEvent(action, $"cell={FormatVector3Int(cell)} height={cell.z}");
    }

    public void RecordBlockedMove(Vector3Int fromCell, Vector3Int toCell, int fromHeight, int toHeight)
    {
        string detail = $"from={FormatVector3Int(fromCell)} to={FormatVector3Int(toCell)} fromHeight={fromHeight} toHeight={toHeight}";
        if (detail == lastBlockedMoveDetail && Time.time - lastBlockedMoveTime < duplicateBlockedMoveSuppressWindow)
        {
            return;
        }

        lastBlockedMoveDetail = detail;
        lastBlockedMoveTime = Time.time;
        RecordEvent("move_blocked", detail);
    }

    public void RecordJumpStarted(Vector3Int fromCell, Vector3Int toCell, int fromHeight, int toHeight)
    {
        RecordEvent("jump_started", $"from={FormatVector3Int(fromCell)} to={FormatVector3Int(toCell)} fromHeight={fromHeight} toHeight={toHeight}");
    }

    public void RecordJumpBlocked(Vector3Int fromCell, Vector3Int toCell, int fromHeight, int toHeight)
    {
        RecordEvent("jump_blocked", $"from={FormatVector3Int(fromCell)} to={FormatVector3Int(toCell)} fromHeight={fromHeight} toHeight={toHeight}");
    }

    public void RecordJumpLanded(Vector3Int landedCell)
    {
        RecordEvent("jump_landed", $"cell={FormatVector3Int(landedCell)}");
    }

    public void RecordEvent(string eventName, string detail)
    {
        if (writer == null)
        {
            return;
        }

        string line = "{"
            + $"\"time\":{Time.time:F3},"
            + $"\"frame\":{Time.frameCount},"
            + $"\"event\":\"{Escape(eventName)}\","
            + $"\"detail\":\"{Escape(detail)}\""
            + "}";

        writer.WriteLine(line);
    }

    private void RecordPlayerSample()
    {
        if (player == null)
        {
            RecordEvent("player_sample", "player=null");
            return;
        }

        Vector3 position = player.position;
        Vector3 samplePosition = position;
        samplePosition.z = 0f;
        IsoWorldChunkManager.GroundCellSample sample = world != null
            ? world.SampleWorldPosition(samplePosition)
            : default;
        RecordEvent(
            "player_sample",
            world != null
                ? $"position={FormatVector3(position)} cell={FormatVector3Int(sample.Cell)} height={sample.Height} transition={sample.IsTransitionCell} edge={sample.IsHeightEdge}"
                : $"position={FormatVector3(position)} cell=0,0,0 height={Mathf.RoundToInt(position.z)}");
    }

    private string Escape(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private string GetRecordingDirectory()
    {
        if (Application.isEditor)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "Logs", "IsoWorldRecordings");
        }

        return Path.Combine(Application.persistentDataPath, "IsoWorldRecordings");
    }

    private string FormatVector2Int(Vector2Int value)
    {
        return $"{value.x},{value.y}";
    }

    private string FormatVector3Int(Vector3Int value)
    {
        return $"{value.x},{value.y},{value.z}";
    }

    private string FormatVector3(Vector3 value)
    {
        return $"{value.x:F3},{value.y:F3},{value.z:F3}";
    }
}
