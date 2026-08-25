namespace GitBinder.Domain.Accounts;

/// <summary>
/// Git 托管平台枚举。仅作为辅助信息展示，不绑定具体平台 API。
/// </summary>
public enum Platform
{
    GitHub,
    Gitee,
    GitLab,
    AliyunCodeup,
    Tencent,
    Custom,
}