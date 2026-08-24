# NetRoute Studio

NetRoute Studio 是基于 .NET 8 与 WPF 开发的 Windows 可视化网络策略管理工具，提供网卡、路由、接口跃点、备份恢复、协议绑定重置和网络诊断能力。

## 功能模块

- 应用基础：依赖注入、日志、管理员权限检测和全局异常处理。
- 网卡管理：查看物理/虚拟网卡、IPv4/IPv6 地址、DNS、网关、状态和接口 Metric。
- 路由查看：分页查看和搜索 IPv4/IPv6 路由。
- 路由匹配：计算目标 IP 或域名的命中路由，并与 Windows 原生结果交叉验证。
- IPv4 路由管理：单条及批量新增、修改、删除，支持临时、永久和在链路上路由。
- 网卡跃点管理：查看和设置 IPv4/IPv6 自动或手动接口 Metric。
- 备份恢复：使用带版本、主机信息和校验摘要的 JSON 备份，对比并选择性恢复路由差异。
- IP 绑定重置：安全重置所选网卡的 IPv4（`ms_tcpip`）或 IPv6（`ms_tcpip6`）绑定。
- 网络测试：DNS、Ping、路由命中及 Tracert 诊断。
- 受控网络命令：白名单、参数校验、实时输出、取消、历史记录和 Telnet 端口测试。

## 运行要求

- Windows 10 或 Windows 11 x64
- 框架依赖发布包需要 .NET 8 Desktop Runtime
- 修改路由、接口 Metric 或协议绑定时需要管理员权限
- Telnet 命令需要启用 Windows Telnet Client 可选功能

建议右键选择“以管理员身份运行”。只读页面可在非管理员模式下使用，但部分网络接口可能因系统权限策略无法读取。

## 安全说明

- 修改或删除路由、重置协议绑定可能立即中断网络连接。
- 修改操作提供命令预览、确认和结果验证。
- 受控命令默认启用白名单；关闭仅在当前窗口有效，且仍禁止命令解释器、脚本宿主、路径启动和连接/重定向字符。
- 路由备份包含主机及网络配置信息，请妥善保管。

## 构建与测试

```powershell
dotnet restore NetRouteStudio.sln --configfile NuGet.Config --disable-parallel
dotnet build NetRouteStudio.sln --no-restore --configuration Debug --maxcpucount:1
dotnet test NetRouteStudio.sln --no-build --configuration Debug --maxcpucount:1
```

生成框架依赖的 Windows x64 发布目录：

```powershell
dotnet publish src/NetRouteStudio.App/NetRouteStudio.App.csproj --no-restore --configuration Release --runtime win-x64 --self-contained false --output artifacts/release/NetRouteStudio-1.0.0-win-x64
```

## 日志与版本

日志写入 `%LOCALAPPDATA%\NetRouteStudio\logs`，按天滚动并保留 14 个文件。

当前稳定版本：`1.0.1`。详细变化见 [CHANGELOG.md](CHANGELOG.md)。

## GitHub Actions 发布

- 推送到 `master`/`main` 或创建 Pull Request 时自动执行 Release 构建和不依赖外部网络环境的稳定测试，并上传临时构建产物。
- 推送 `v*` 标签（例如 `v1.0.1`）时，标签版本会自动写入程序集，生成 `win-x64` 自包含 ZIP、SHA-256 校验文件和 GitHub Release。
- 也可以在 Actions 页面手动运行工作流并填写 `v1.0.1` 格式的版本号。
- 依赖真实 Windows 网卡、路由、DNS 和公网的集成测试在手动运行及每周计划任务中单独执行；外部网络限制导致的失败不会阻止正式发布。

```powershell
git tag v1.0.1
git push origin v1.0.1
```
