using System.Text.Json.Serialization;

namespace UnoApp1.Models;

public class KeyData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("ad")]
    public int AnalogValue { get; set; }

    [JsonPropertyName("pressed")]
    public bool IsPressed { get; set; }
}

public class AnalogValueMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "analog_values";

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("keys")]
    public List<KeyData> Keys { get; set; } = new();
}

public class StatusMessage
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}

public static class SerialCommands
{
    public const string StartAnalog = "START_ANALOG";
    public const string StopAnalog = "STOP_ANALOG";
    public const string Heartbeat = "HEARTBEAT";
}
