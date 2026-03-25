namespace Weixin.Bot.Sdk.Models.Wire;

public sealed class GetUpdatesResponse
{
    [JsonPropertyName("ret")] public int ReturnCode { get; set; }
    [JsonPropertyName("errcode")] public int ErrorCode { get; set; }
    [JsonPropertyName("get_updates_buf")] public string? GetUpdatesBuffer { get; set; }
    [JsonPropertyName("msgs")] public List<MessagePayload>? Messages { get; set; }
}

public sealed class MessagePayload
{
    [JsonPropertyName("message_id")] public string? MessageId { get; set; }
    [JsonPropertyName("from_user_id")] public string? FromUserId { get; set; }
    [JsonPropertyName("to_user_id")] public string? ToUserId { get; set; }
    [JsonPropertyName("create_time_ms")] public long? CreatedAtMs { get; set; }
    [JsonPropertyName("context_token")] public string? ContextToken { get; set; }
    [JsonPropertyName("message_type")] public MessageType MessageType { get; set; }
    [JsonPropertyName("message_state")] public MessageState MessageState { get; set; }
    [JsonPropertyName("item_list")] public List<MessageItemPayload>? Items { get; set; }
}

public sealed class MessageItemPayload
{
    [JsonPropertyName("type")] public MessageItemType Type { get; set; }
    [JsonPropertyName("text_item")] public TextItemPayload? TextItem { get; set; }
    [JsonPropertyName("image_item")] public ImageItemPayload? ImageItem { get; set; }
    [JsonPropertyName("voice_item")] public VoiceItemPayload? VoiceItem { get; set; }
    [JsonPropertyName("file_item")] public FileItemPayload? FileItem { get; set; }
    [JsonPropertyName("video_item")] public VideoItemPayload? VideoItem { get; set; }
    [JsonPropertyName("ref_msg")] public ReferencedMessagePayload? ReferencedMessage { get; set; }
}

public sealed class TextItemPayload
{
    [JsonPropertyName("text")] public string? Text { get; set; }
}

public sealed class ImageItemPayload
{
    [JsonPropertyName("media")] public MediaPayload? Media { get; set; }
    [JsonPropertyName("mid_size")] public string? MidSize { get; set; }
    [JsonPropertyName("aeskey")] public string? AesKey { get; set; }
}

public sealed class VoiceItemPayload
{
    [JsonPropertyName("media")] public MediaPayload? Media { get; set; }
    [JsonPropertyName("encode_type")] public VoiceEncodeType EncodeType { get; set; } = VoiceEncodeType.Silk;
    [JsonPropertyName("sample_rate")] public int SampleRate { get; set; } = 24000;
    [JsonPropertyName("bits_per_sample")] public int BitsPerSample { get; set; } = 16;
    [JsonPropertyName("playtime")] public int Playtime { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
}

public sealed class FileItemPayload
{
    [JsonPropertyName("media")] public MediaPayload? Media { get; set; }
    [JsonPropertyName("file_name")] public string? FileName { get; set; }
    [JsonPropertyName("len")] public string? Length { get; set; }
}

public sealed class VideoItemPayload
{
    [JsonPropertyName("media")] public MediaPayload? Media { get; set; }
    [JsonPropertyName("video_size")] public string? VideoSize { get; set; }
}

public sealed class MediaPayload
{
    [JsonPropertyName("encrypt_query_param")] public string? EncryptQueryParam { get; set; }
    [JsonPropertyName("aes_key")] public string? AesKey { get; set; }
    [JsonPropertyName("encrypt_type")] public int? EncryptType { get; set; }
}

public sealed class ReferencedMessagePayload
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("message_item")] public MessageItemPayload? MessageItem { get; set; }
}
