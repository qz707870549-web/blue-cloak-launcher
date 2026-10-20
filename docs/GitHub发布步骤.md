# 把蓝斗篷启动发布到 GitHub

你会使用两个 ZIP：

| 文件 | 用途 |
| --- | --- |
| `BlueCloakLauncher-v1.3.8-public-source.zip` | 解压后上传内容到代码仓库 |
| `BlueCloakLauncher-v1.3.8-public-Windows-x64.zip` | 作为 Releases 附件，给普通用户下载运行 |
| 运行包对应的 `.sha256` | 可选的下载完整性校验文件 |

只使用这两个**公开版**压缩包。之前“个人版”“内置分组版”等文件包含个人配置，不用于公开发布。

## 一、解压公开源码

1. 将 `BlueCloakLauncher-v1.3.8-public-source.zip` 解压到一个新的空文件夹，例如“蓝斗篷启动公开源码”。
2. 打开后应能直接看到 `README.md`、`LICENSE`、`.gitignore`、`build.ps1`、`src`、`tests`、`docs` 等。
3. 根目录不要再套一层同名文件夹。不要把源码 ZIP 本身上传成唯一代码文件。
4. 本包已设置 MIT 代码许可证。发布者应确认协作者同意公开其贡献，头像可以分发；字体自带 OFL 许可证，不要删除。

## 二、创建公开仓库

1. 登录 [GitHub](https://github.com/)。
2. 进入 [创建仓库](https://github.com/new)。
3. Repository name 填 `blue-cloak-launcher`。
4. Description 可填：`蓝斗篷启动：Windows 应用分组、网站与文件夹快捷启动工具，支持开始按钮和 Win 键呼出。`
5. 选择 **Public**。
6. 不勾选初始化 README；Add .gitignore 和 Choose a license 选择 None，因为文件已经在源码包中。
7. 点击 **Create repository**。

参考：[GitHub 创建仓库说明](https://docs.github.com/en/repositories/creating-and-managing-repositories/creating-a-new-repository)。

## 三、通过网页上传源码（第一次发布推荐）

1. 在刚创建的空仓库中点击 **uploading an existing file**。已有文件的仓库点击 **Add file → Upload files**。
2. 打开刚解压的干净源码文件夹，将其中的文件和 `src`、`tests`、`docs` 文件夹一起拖到上传区域。
3. 等待上传完成。应能看到 `.gitignore`、`LICENSE`、`README.md` 和 `src/Program.cs` 等路径。
4. 确认没有 `data`、`personal-data`、`artifacts`、个人备份、日志或其他历史压缩包。网页上传不会替你执行 `.gitignore` 的过滤逻辑。
5. 提交说明填：`Initial public release v1.3.8`。
6. 初次上传到默认分支，点击页面上的 **Commit changes**；若界面要求 Propose changes，则按界面提示创建并合并提交。
7. 返回仓库首页，README 应自动显示，`src` 文件夹可直接打开阅读代码。

GitHub 网页单次最多上传 100 个文件，单文件上限 25 MiB。本公开源码包按此方式整理；若以后文件增多，请使用下面的 Git 方法。

参考：[GitHub 上传文件说明](https://docs.github.com/en/repositories/working-with-files/managing-files/adding-a-file-to-a-repository)。

## 四、发布可运行的软件

1. 在仓库首页右侧找到 **Releases**，点击进入，再点 **Draft a new release** / **Create a new release**。
2. 在 Choose a tag 输入 `v1.3.8`，选择创建新标签，Target 选 `main`。
3. Release title 填：`蓝斗篷启动 v1.3.8 公开预览版`。
4. 将 `docs/RELEASE-v1.3.8.md` 中的内容复制到发布说明。
5. 上传 `BlueCloakLauncher-v1.3.8-public-Windows-x64.zip` 和其 `.sha256` 文件。
6. 第一次公开发布建议勾选 **This is a pre-release**：目前验证主要是离线检查，仍需收集不同电脑上的实际使用反馈。
7. 检查附件上传完成后点击 **Publish release**；需要稍后检查则先 **Save draft**。
8. 用普通用户视角打开 Releases，确认下载的是运行 ZIP，而不是自动生成的 Source code。

参考：[GitHub 创建 Release 说明](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)。

## 五、补充项目首页信息

- 在 About 的设置按钮中填写介绍，添加 `windows`、`wpf`、`launcher`、`csharp` 等主题。
- 在 README 顶部添加你发布后的 Releases 链接。仓库地址确定之前不要保留虚构用户名。
- 添加一张使用通用示例数据的截图；不要把含有私人网站、账号、本机路径的截图上传。
- 问题通过 Issues 收集。发布后把朋友的实际贡献补充到贡献说明，可邀请朋友作为 Collaborator。

## 六、以后更新版本

1. 修改源码，用 `verify.ps1` 检查。
2. 更新 `src/VersionInfo.cs`、设置中的显示版本、`package.ps1` 的发行文件名、README 与版本说明。
3. 使用 `package.ps1` 生成新的公开运行包。该脚本采用文件白名单，不复制你的运行数据。
4. 提交代码，再创建新的版本标签和 Release，例如 `v1.3.9`。
5. 网页适合第一次少量上传。长期维护建议采用 Git 或 GitHub Desktop，避免遗漏删除文件。

## 可选：使用 Git 上传

安装 Git for Windows 后，在刚解压的**干净公开源码**文件夹打开 PowerShell。将下方 `YOUR_USERNAME` 替换成你的实际 GitHub 用户名。此方法要求第二步创建的是空仓库。

```powershell
git init -b main
git add .
git status --short
git commit -m "Initial public release v1.3.8"
git remote add origin https://github.com/YOUR_USERNAME/blue-cloak-launcher.git
git push -u origin main
```

第一次提交若提示身份未配置，在该仓库中用你自己的名字及 GitHub 提供的 noreply 邮箱配置后再提交。不要在聊天中粘贴令牌或密码。推送时按 Git 的浏览器登录提示完成认证。

如果已经通过网页上传，不要再对同一个项目按上述流程新建不相关历史；应先克隆仓库，再在克隆目录中修改和提交。

## 发布前最后检查

- 上传的是公开源码包，不是整个开发文件夹。
- README、LICENSE、字体授权文件都在。
- 没有私人备份、分组、网站、本机路径、账号或密钥。
- Release 有可运行 ZIP，使用说明明确依赖 .NET Framework 4.8。
- 没有声称完成未执行过的真实开机或多设备测试。
