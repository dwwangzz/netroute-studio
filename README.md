# NetRoute Studio

Windows 可视化网络策略管理应用。

## 开发环境

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022（可选，需安装“.NET 桌面开发”工作负载）

## 构建与测试

```powershell
dotnet restore NetRouteStudio.sln --configfile NuGet.Config --disable-parallel
dotnet build NetRouteStudio.sln --no-restore --maxcpucount:1
dotnet test NetRouteStudio.sln --no-build --maxcpucount:1
```

当前解决方案包含 WPF 主项目及引用它的测试项目。使用单 MSBuild 节点可避免部分 Windows 环境并行调度 WPF 构建时出现无错误退出。

## 日志位置

运行日志默认写入：

```text
%LOCALAPPDATA%\NetRouteStudio\logs
```

日志按天滚动，默认保留 14 个文件。

## 当前进度

第 1 模块“应用基础模块”：已验收。

第 2 模块“Windows 网络读取基础设施”：已验收。

第 3 模块“网卡管理”：已验收。

第 4 模块“路由只读管理”：已验收。

第 5 模块补充：支持域名解析后的全部 IPv4/IPv6 地址匹配。

第 5 模块“路由匹配”：已验收。

第 6 模块“IPv4 路由管理（单条操作）”：等待人工验收。
