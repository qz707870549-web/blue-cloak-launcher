# 把蓝斗篷启动 v1.3.21 更新到 GitHub

更新分为源码提交和发行附件上传两步。先完成两批源码提交，再创建 v1.3.21 标签，确保标签对应完整代码。

## 准备公开文件

| 文件 | 用途 |
| --- | --- |
| `BlueCloakLauncher-v1.3.21-public-source.zip` | 解压后上传源码内容 |
| `蓝斗篷启动-v1.3.21-Windows-x64-安装程序.exe` | Releases 附件，提供简体中文安装向导 |
| `BlueCloakLauncher-v1.3.21-public-Windows-x64.zip` | Releases 附件，提供便携运行方式 |
| 两个运行文件对应的 `.sha256` | 完整性校验附件 |

只从本次公开源码 ZIP 解压出干净文件，不上传整个开发文件夹、个人版、个人备份、data、personal-data、核对记录、缓存或日志。

## 一、解压源码

1. 将源码 ZIP 解压到新建空文件夹。
2. 进入能直接看到 README.md、build.ps1、build-installer.ps1、src、tests、docs、installer 的这一层。
3. 上传这一层的内容，不上传外层目录，也不把源码 ZIP 当成唯一代码文件。

## 二、网页分两批更新源码

GitHub 网页单次最多上传 100 个文件，单文件上限 25 MiB。本版源码超过 100 个文件，不能一次全选上传。

第一批：

1. 打开你的仓库首页，确认分支为 main。
2. 点击 Add file → Upload files。
3. 只把解压目录中的 src 文件夹拖入上传区域，保留 src/ 开头的路径。
4. 等待完成，提交说明填写 Update application source to v1.3.21，提交到 main。

第二批：

1. 回到仓库首页，再次点击 Add file → Upload files。
2. 从同一个解压目录选择除 src 之外的所有文件和文件夹，包括 tests、docs、installer、README.md、build-installer.ps1 等。
3. 等待完成，提交说明填写 Update tests, installer and docs for v1.3.21，提交到 main。
4. 返回首页，确认 README 显示 v1.3.21；打开 installer/launcher.nsi、src/VersionInfo.cs、tests/WorkspaceStyleTests.cs，确认文件齐全。
5. 完成两批后再发布 Release。网页上传不会自动删除旧文件；本次已有文件使用同路径更新，后续版本若删除或移动代码，应采用 Git 管理并核对差异。

官方说明：https://docs.github.com/en/repositories/working-with-files/managing-files/adding-a-file-to-a-repository

## 三、发布软件下载

1. 仓库首页 → Releases → Draft a new release。
2. Choose a tag 输入 v1.3.21，选择 Create new tag；Target 选择 main。
3. 标题填写：蓝斗篷启动 v1.3.21 公开预览版。
4. 将 docs/RELEASE-v1.3.21.md 的内容复制到说明框。
5. 上传安装 EXE、便携 ZIP，以及它们对应的两个 .sha256 文件。
6. 等待全部附件上传完成，勾选 This is a pre-release。当前安装程序只完成构建和离线检查，尚未真实安装验证。
7. 核对标签、说明和附件版本都为 v1.3.21，再点击 Publish release。
8. 发布后打开 Releases 页面，确认用户可以看到安装程序与便携包。GitHub 自动生成的 Source code 是源码，需要自行编译。

官方说明：https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository

## 四、分享和用户升级

给粉丝分享仓库的 Releases 页面即可；该链接以后继续更新时不用改变。项目主页适合介绍开源代码。

安装版升级：先导出备份，通过托盘退出旧版，再安装到原安装目录。便携版迁移：先导出 .lumabackup，安装后在设置中导入。便携版原目录升级：覆盖发行程序和资源，保留原数据目录、个人种子、自定义图标和网站资源。

## 后续版本

长期维护建议使用 Git 或 GitHub Desktop。先拉取远程 main，覆盖需要更新的源码，检查新增、修改和删除文件，提交并推送；随后再创建对应版本标签与 Release。不要对已有仓库反复初始化独立 Git 历史，不要在公开代码中加入令牌或签名证书私钥。