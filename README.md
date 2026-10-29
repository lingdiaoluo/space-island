<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4.svg" alt=".NET 8" />
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4.svg" alt="platform" />
  <img src="https://img.shields.io/badge/license-MIT-16a34a.svg" alt="license" />
</p>

# 空间岛 SpaceIsland

一个轻量的 Windows C 盘清理小工具 —— 只清理「删掉之后会自动重建」的缓存和临时文件，把真正该由你决定的内容单独拎出来让你确认，绝不碰你的文档和照片。

单文件、免安装，双击就能用，不需要 Python 或任何运行时。

## 特性

- **一键扫描**：启动即扫，把 C 盘能释放的空间按风险分成三档
- **三级分诊**：🟢 可直接清理 / 🟡 需要确认 / 🔵 不要手删，一眼看清什么能删什么不能删
- **删除前必确认**：点「清理」会弹出确认窗，逐条列出要删的项，明确标注哪些清掉后要重新下载
- **不误删**：代码里内置护栏 —— 盘符根、Windows、Program Files、用户目录本身一律拒绝操作，符号链接/联接点自动跳过
- **回收站走系统接口**：清空回收站调用 Windows 原生 API，而不是暴力删目录
- **深浅双主题**：右上角一键切换

## 能清理什么

| 分组 | 内容 | 说明 |
| --- | --- | --- |
| 🟢 可直接清理 | 用户临时文件、Windows 临时文件、更新缓存、传递优化缓存、回收站、缩略图缓存、崩溃转储、错误报告存档、CBS 日志、显卡着色器缓存、浏览器网页缓存 | 删掉会自动重建 |
| 🟡 需要确认 | pip / npm / NuGet 下载缓存 | 清掉后下次要重新联网下载 |
| 🔵 仅展示 | WinSxS 组件存储、Windows.old | 建议走系统正规入口，不提供删除按钮 |

> 默认只勾选 🟢 安全项。🟡 需要你手动勾选才会参与清理。

## 安全设计

删除动作前，每个路径都会经过三道检查：

1. **清单本身保守** —— 只收录会重建的缓存，你的文档、图片、下载文件根本不在表里
2. **确认弹窗** —— 清理前逐条展示，必须点「允许」才执行
3. **代码护栏** —— `PathGuard` 拦截系统关键目录，`Cleaner` 只删除目标目录内部的文件，遇到符号链接/联接点直接跳过

## 使用

到 [Releases](https://github.com/lingdiaoluo/space-island/releases/latest) 下载 `SpaceIsland.exe`（约 75 MB），双击运行即可。

- 打开后自动扫描，顶部显示 C 盘占用和「可释放」合计
- 想清掉黄色项就手动勾选，然后点「开始清理」
- 首次会弹确认窗，点「允许清理」才真正执行

## 系统要求

- Windows 10（1809+）/ Windows 11，64 位
- 清理 `Windows\Temp`、更新缓存等系统目录时，建议以管理员身份运行，否则会跳过这些项

## 构建

```bash
git clone https://github.com/lingdiaoluo/space-island.git
cd space-island
dotnet publish SpaceIsland.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true
```

## 许可证

[MIT](LICENSE)
