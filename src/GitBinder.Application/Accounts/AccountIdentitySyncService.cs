using GitBinder.Application.Bindings;
using GitBinder.Application.GlobalMode;
using GitBinder.Domain.Common;
using GitBinder.Domain.Services;

namespace GitBinder.Application.Accounts;

/// <summary>账号保存后，按当前有效账号规则同步提交身份。</summary>
public sealed class AccountIdentitySyncService
{
    private readonly BindingService _bindingService;
    private readonly GlobalModeService _globalModeService;
    private readonly EffectiveAccountResolver _effectiveAccountResolver;

    public AccountIdentitySyncService(
        BindingService bindingService,
        GlobalModeService globalModeService,
        EffectiveAccountResolver effectiveAccountResolver)
    {
        _bindingService = bindingService;
        _globalModeService = globalModeService;
        _effectiveAccountResolver = effectiveAccountResolver;
    }

    public async Task<Result> SyncAsync(Guid accountId, CancellationToken ct = default)
    {
        var config = await _globalModeService.GetConfigAsync(ct);
        if (config.Enabled && _effectiveAccountResolver.Resolve(null)?.Id == accountId)
        {
            // 当前全局账号需同时更新全局与绑定仓库，沿用原快照和应用流程。
            var result = await _globalModeService.EnsureAppliedAsync(ct);
            if (!result.IsSuccess)
                return result;

            // 启动/全局模式流程允许略过失效路径；保存时仍需逐仓库反馈未同步项。
        }

        return await _bindingService.ReapplyAccountIdentityAsync(accountId, ct);
    }
}
