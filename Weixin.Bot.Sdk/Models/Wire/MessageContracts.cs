namespace Weixin.Bot.Sdk.Models.Wire;

internal sealed class GetUpdatesResponse
{
    [JsonPropertyName("ret")] public int ReturnCode { get; set; }
    [JsonPropertyName("errcode")] public int ErrorCode { get; set; }
    [JsonPropertyName("get_updates_buf"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? GetUpdatesBuffer { get; set; }
    [JsonPropertyName("msgs")] public List<MessagePayload>? Messages { get; set; }
}

internal sealed class MessagePayload
{
    [JsonPropertyName("message_id"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? MessageId { get; set; }
    [JsonPropertyName("from_user_id"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? FromUserId { get; set; }
    [JsonPropertyName("to_user_id"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? ToUserId { get; set; }
    [JsonPropertyName("create_time_ms")] public long? CreatedAtMs { get; set; }
    [JsonPropertyName("context_token"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? ContextToken { get; set; }
    [JsonPropertyName("message_type")] public MessageType MessageType { get; set; }
    [JsonPropertyName("message_state")] public MessageState MessageState { get; set; }
    [JsonPropertyName("item_list")] public List<MessageItemPayload>? Items { get; set; }
}

internal sealed class MessageItemPayload
{
    [JsonPropertyName("type")] public MessageItemType Type { get; set; }
    [JsonPropertyName("text_item")] public TextItemPayload? TextItem { get; set; }
    [JsonPropertyName("image_item")] public ImageItemPayload? ImageItem { get; set; }
    [JsonPropertyName("voice_item")] public VoiceItemPayload? VoiceItem { get; set; }
    [JsonPropertyName("file_item")] public FileItemPayload? FileItem { get; set; }
    [JsonPropertyName("video_item")] public VideoItemPayload? VideoItem { get; set; }
    [JsonPropertyName("ref_msg")] public ReferencedMessagePayload? ReferencedMessage { get; set; }
}

internal sealed class TextItemPayload
{
    [JsonPropertyName("text"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? Text { get; set; }
}

internal sealed class ImageItemPayload
{
    [JsonPropertyName("media")] public MediaPayload? Media { get; set; }
    [JsonPropertyName("mid_size")] public int? MidSize { get; set; }
    [JsonPropertyName("aeskey"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? AesKey { get; set; }
}

internal sealed class VoiceItemPayload
{
    [JsonPropertyName("media")] public MediaPayload? Media { get; set; }
    [JsonPropertyName("encode_type")] public VoiceEncodeType EncodeType { get; set; } = VoiceEncodeType.Silk;
    [JsonPropertyName("sample_rate")] public int SampleRate { get; set; } = 24000;
    [JsonPropertyName("bits_per_sample")] public int BitsPerSample { get; set; } = 16;
    [JsonPropertyName("playtime")] public int Playtime { get; set; }
    [JsonPropertyName("text"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? Text { get; set; }
}

internal sealed class FileItemPayload
{
    [JsonPropertyName("media")] public MediaPayload? Media { get; set; }
    [JsonPropertyName("file_name"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? FileName { get; set; }
    [JsonPropertyName("len"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? Length { get; set; }
}

internal sealed class VideoItemPayload
{
    [JsonPropertyName("media")] public MediaPayload? Media { get; set; }
    [JsonPropertyName("video_size")] public int? VideoSize { get; set; }
}

internal sealed class MediaPayload
{
    [JsonPropertyName("encrypt_query_param"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? EncryptQueryParam { get; set; }
    [JsonPropertyName("aes_key"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? AesKey { get; set; }
    [JsonPropertyName("encrypt_type")] public int? EncryptType { get; set; }
}

internal sealed class ReferencedMessagePayload
{
    [JsonPropertyName("title"), JsonConverter(typeof(FlexibleStringJsonConverter))] public string? Title { get; set; }
    [JsonPropertyName("message_item")] public MessageItemPayload? MessageItem { get; set; }
}
