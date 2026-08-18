# Essentials Installer

**Essentials 库的一键初始化工具（Unity Editor 包）**

Essentials Installer 是一个 Unity 编辑器扩展包，用于解决 Essentials 库对多个 Package 的依赖问题：它会在项目首次加载时自动弹出初始化窗口，检测并引导安装 Essentials 所需的全部依赖，并支持安装可选的 Essentials Services。

> 纯编辑器工具，不包含任何运行时代码，不会影响玩家构建。

---

## 目录

- [功能特性](#功能特性)
- [环境要求](#环境要求)
- [安装](#安装)
- [使用方法](#使用方法)
- [依赖与可选服务](#依赖与可选服务)
- [工作原理](#工作原理)
- [常见问题](#常见问题)
- [项目结构](#项目结构)
- [许可证](#许可证)

---

## 功能特性

- **自动弹出**：项目首次加载（域重载后）自动打开初始化窗口，每个项目只自动弹出一次。
- **依赖检测**：自动识别 Addressables、DOTween、Essentials DI 是否已安装。
- **一键安装**：点击按钮按队列顺序安装所有缺失依赖，同一时间只发出一个 Package Manager 请求，避免并发冲突。
- **多来源支持**：支持 Unity 官方包、Git 仓库（含版本标签）以及需要手动安装的 Asset Store 外部资源。
- **可选服务**：依赖全部就绪后，可安装可选的 Essentials Services。
- **状态可视化**：安装进度条、旋转动画、状态胶囊（Setup Required / Ready / Complete）、依赖/服务计数、错误提示条与 Recheck 按钮。
- **状态周期复查**：每 2 秒自动复查一次安装状态，手动安装外部依赖后无需重启即可识别。

---

## 环境要求

- Unity **2022.3** 及以上
- 仅编辑器（Editor）环境使用

---

## 安装

通过 Unity Package Manager 将本包加入项目。在项目根目录的 `Packages/manifest.json` 的 `dependencies` 中添加（以 Git 或本地路径方式均可）：

```json
{
  "dependencies": {
    "com.sug.essentials-installer": "https://github.com/Sugar0612/Essentials-Installer.git"
  }
}
```

> 本地开发时可将 URL 替换为本地路径，例如 `"com.sug.essentials-installer": "file:../Essentials Installer"`。
> Git 地址请以实际仓库为准。

也可以使用 Unity 的 **Window ▸ Package Manager ▸ + ▸ Add package from git URL…** 进行添加。

---

## 使用方法

1. **首次加载项目**：初始化窗口会自动弹出（每个项目仅一次）。
2. **手动打开**：菜单 **Tools ▸ Essentials ▸ Initialization**。
3. 在窗口中：
   - 点击单个依赖右侧的 **Install** 安装对应依赖；
   - 点击 **Install Missing Dependencies** 一键安装所有缺失依赖；
   - DOTween 为 Asset Store 外部资源，按钮为 **Open Page**，点击后打开商店页面，需要手动安装；
   - 依赖全部安装完成后，可在服务区点击 **Install** 安装可选的 **Essentials Services**。
4. 安装状态会自动刷新；也可点击右下角 **Recheck** 立即重新检测。

---

## 依赖与可选服务

### 必需依赖

| 名称 | 包标识 | 来源 | 安装方式 |
| --- | --- | --- | --- |
| Addressables | `com.unity.addressables` | Unity 官方 | UPM 自动安装 |
| DOTween | `DOTween`（程序集名） | 外部（Asset Store） | 手动安装，工具打开商店页 |
| Essentials DI | `com.sug.essentials` | Git（GitHub，tag `1.1.0`） | UPM 自动安装 |

### 可选服务

| 名称 | 包标识 | 来源 | 说明 |
| --- | --- | --- | --- |
| Essentials Services | `com.sug.essentials.services` | Git（GitHub，tag `1.0.0`） | 依赖全部安装后才能安装 |

---

## 工作原理

### 安装流程

- 所有 UPM 安装请求共用一个请求槽，**同一时间只处理一个包**，通过内部队列依次执行；
- 依赖安装完成后自动继续队列中的下一个；外部依赖（如 DOTween）直接打开页面，不阻塞队列；
- 窗口关闭时请求继续在后台完成，重新打开窗口会以干净的初始状态重新检测。

### 安装状态检测

- **Unity 官方包 / Git 包**：通过 `UnityEditor.PackageManager.PackageInfo.FindForPackageName` 按包名查询；
- **Git 包兜底**：Git 包的 package.json 名称可能与预期不一致，此时回退读取 `Packages/manifest.json`，按源码 URL（去掉 `#tag` 片段）进行匹配；
- **外部资源（DOTween）**：扫描当前 AppDomain 中已加载的程序集，按程序集名 `DOTween` 匹配。

---

## 常见问题

**Q：安装完成后提示 “Package Manager reported a failure for an untracked request”？**
这不是安装失败。安装含代码的 Git 包会触发程序域重载，期间请求的跟踪信息会丢失，UPM 可能以 `Failure` 状态返回且不带错误信息——即使包已经成功装入 `manifest.json`。窗口会忽略该请求并重新检测真实状态，卡片会正确显示为已安装。

**Q：窗口没有自动弹出？**
每个项目只在首次加载时自动弹出一次（按项目路径记录）。之后请通过 **Tools ▸ Essentials ▸ Initialization** 手动打开。

**Q：DOTween 显示未安装，点击后只是打开网页？**
DOTween 是 Asset Store 外部资源，无法通过 UPM 自动安装。点击 **Open Page** 打开商店页面手动导入即可；安装完成后窗口会在复查时自动识别（若未识别，点击 **Recheck**）。

**Q：为什么一次只安装一个包？**
Package Manager 同一时间只支持一个进行中的请求。工具通过队列串行安装，避免并发冲突。

---

## 项目结构

```
Essentials Installer/
├── package.json                                  # UPM 包清单
├── Editor/
│   ├── EssentialsAutoInitializer.cs              # 项目首次加载时自动弹出初始化窗口
│   ├── EssentialsInitializerWindow.cs            # 初始化窗口主体（检测 / 安装 / UI）
│   └── SUG.EssentialsInstaller.Editor.asmdef     # 编辑器专用程序集定义
├── LICENSE                                       # 许可证
└── README.md
```

---

## 许可证

`package.json` 中声明为 **MIT**。

> ⚠️ 注意：仓库中的 `LICENSE` 文件内容为 Apache License 2.0，与 `package.json` 声明的 MIT 不一致。发布前请确认并统一两者。
