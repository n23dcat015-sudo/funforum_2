using Funforum.Api.Dtos.Appeals;

namespace Funforum.Api.Services;

// X.5 — user xem trạng thái kháng cáo của mình và nộp đơn cho lệnh ban đang áp dụng.
public interface IAppealService
{
    Task<MyAppealDto> GetMineAsync(int userId, CancellationToken ct);

    Task<MyAppealDto> CreateAsync(CreateAppealRequest request, int userId, CancellationToken ct);
}
