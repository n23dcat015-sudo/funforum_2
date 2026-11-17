using Funforum.Api.Dtos.Attachments;
using Funforum.Api.Entities;

namespace Funforum.Api.Common;

public static class AttachmentMapper
{
    // current_state.md mục 3.7: URL công khai /uploads/images/{storedName} và
    // /uploads/thumbs/{thumbName}.
    public static AttachmentDto ToDto(Attachment a) => new()
    {
        Url = $"/uploads/images/{a.StoredName}",
        ThumbUrl = $"/uploads/thumbs/{a.ThumbName}",
        OriginalName = a.OriginalName,
        Width = a.Width,
        Height = a.Height,
        SizeBytes = a.SizeBytes,
    };
}