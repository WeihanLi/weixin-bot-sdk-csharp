namespace Weixin.Bot.Sdk.Models.Wire;

internal sealed class BaseInfoPayload
{
    [JsonPropertyName("channel_version")] public string ChannelVersion { get; init; } = string.Empty;
}

internal sealed class GetUpdatesRequest
{
    [JsonPropertyName("get_updates_buf")] public string GetUpdatesBuffer { get; init; } = string.Empty;
    [JsonPropertyName("base_info")] public BaseInfoPayload BaseInfo { get; init; } = new();
}

internal sealed class SendMessageRequest
{
    [JsonPropertyName("msg")] public OutboundMessagePayload Message { get; init; } = new();
    [JsonPropertyName("base_info")] public BaseInfoPayload BaseInfo { get; init; } = new();
}

internal sealed class OutboundMessagePayload
{
    [JsonPropertyName("from_user_id")] public string FromUserId { get; init; } = string.Empty;
    [JsonPropertyName("to_user_id")] public string ToUserId { get; init; } = string.Empty;
    [JsonPropertyName("client_id")] public string ClientId { get; init; } = string.Empty;
    [JsonPropertyName("message_type")] public int MessageType { get; init; }
    [JsonPropertyName("message_state")] public int MessageState { get; init; }
    [JsonPropertyName("item_list")] public MessageItemPayload[] Items { get; init; } = [];
    [JsonPropertyName("context_token")] public string ContextToken { get; init; } = string.Empty;
}

internal sealed class SendTypingRequest
{
    [JsonPropertyName("ilink_user_id")] public string UserId { get; init; } = string.Empty;
    [JsonPropertyName("typing_ticket")] public string TypingTicket { get; init; } = string.Empty;
    [JsonPropertyName("status")] public int Status { get; init; }
    [JsonPropertyName("base_info")] public BaseInfoPayload BaseInfo { get; init; } = new();
}

internal sealed class GetUploadUrlRequest
{
    [JsonPropertyName("filekey")] public string FileKey { get; init; } = string.Empty;
    [JsonPropertyName("media_type")] public int MediaType { get; init; }
    [JsonPropertyName("to_user_id")] public string ToUserId { get; init; } = string.Empty;
    [JsonPropertyName("rawsize")] public int RawSize { get; init; }
    [JsonPropertyName("rawfilemd5")] public string RawFileMd5 { get; init; } = string.Empty;
    [JsonPropertyName("filesize")] public int FileSize { get; init; }
    [JsonPropertyName("no_need_thumb")] public bool NoNeedThumb { get; init; } = true;
    [JsonPropertyName("aeskey")] public string AesKey { get; init; } = string.Empty;
    [JsonPropertyName("base_info")] public BaseInfoPayload BaseInfo { get; init; } = new();
}

internal sealed class GetConfigRequest
{
    [JsonPropertyName("ilink_user_id")] public string UserId { get; init; } = string.Empty;
    [JsonPropertyName("context_token")] public string ContextToken { get; init; } = string.Empty;
    [JsonPropertyName("base_info")] public BaseInfoPayload BaseInfo { get; init; } = new();
}
