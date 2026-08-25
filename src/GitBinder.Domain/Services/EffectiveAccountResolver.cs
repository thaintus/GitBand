using GitBinder.Domain.Accounts;
using GitBinder.Domain.Projects;

namespace GitBinder.Domain.Services;

/// <summary>
/// 有效账号解析器。任何 Git 身份相关操作都必须经由它统一决策。
/// 规则：Global Mode > Project Binding > Default Account。
/// </summary>
public sealed class EffectiveAccountResolver
{
    private readonly IGlobalModeState _globalModeState;
    private readonly IBindingReader _bindingReader;
    private readonly IAccountReader _accountReader;

    public EffectiveAccountResolver(
        IGlobalModeState globalModeState,
        IBindingReader bindingReader,
        IAccountReader accountReader)
    {
        _globalModeState = globalModeState;
        _bindingReader = bindingReader;
        _accountReader = accountReader;
    }

    /// <summary>解析指定仓库路径的有效账号。</summary>
    public Account? Resolve(Project? project)
    {
        // 1. Global Mode 优先级最高。
        if (_globalModeState.IsEnabled)
        {
            var globalId = _globalModeState.GlobalAccountId;
            if (globalId is Guid g)
            {
                var account = _accountReader.GetById(g);
                if (account is not null && account.Enabled)
                {
                    return account;
                }
            }

            // 回退到默认账号。
            return _accountReader.GetDefault();
        }

        // 2. Project Binding。
        if (project is not null)
        {
            var bindingAccountId = _bindingReader.GetAccountIdByProject(project.Id);
            if (bindingAccountId is Guid id)
            {
                var account = _accountReader.GetById(id);
                if (account is not null && account.Enabled)
                {
                    return account;
                }
            }
        }

        // 3. Default Account。
        return _accountReader.GetDefault();
    }

    /// <summary>解析原因描述码，供 GUI 展示 Status。</summary>
    public EffectiveReason ResolveReason(Project? project)
    {
        if (_globalModeState.IsEnabled)
        {
            return EffectiveReason.GlobalMode;
        }

        if (project is not null && _bindingReader.GetAccountIdByProject(project.Id) is not null)
        {
            return EffectiveReason.Binding;
        }

        return EffectiveReason.Default;
    }
}

/// <summary>有效账号来源。</summary>
public enum EffectiveReason
{
    GlobalMode,
    Binding,
    Default,
}

/// <summary>Global Mode 状态读取。</summary>
public interface IGlobalModeState
{
    bool IsEnabled { get; }
    Guid? GlobalAccountId { get; }
}

/// <summary>绑定关系读取。</summary>
public interface IBindingReader
{
    Guid? GetAccountIdByProject(Guid projectId);
}

/// <summary>账号读取。</summary>
public interface IAccountReader
{
    Account? GetById(Guid id);
    Account? GetDefault();
}