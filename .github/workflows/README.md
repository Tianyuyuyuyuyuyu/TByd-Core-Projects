# GitHub Actions 工作流说明

## UPM包同步工作流

此目录包含自动将开发仓库中的UPM包同步到包仓库的GitHub Actions工作流配置。

### sync-upm-packages.yml

此工作流实现了将开发仓库中所有符合命名规则的UPM包自动同步到包仓库。

#### 同步规则

工作流会查找并同步符合以下规则的UPM包：
- 开发仓库中所有以"TByd."开头的文件夹
- 这些文件夹的Assets目录下所有以"TByd."开头的文件夹（大小写敏感）

#### 目标路径规则

- 源路径：`TByd.XXX/Assets/TByd.YYY/*`
- 目标路径：`Packages/com.tbyd.yyy/*`（将包名转为小写）

#### 同步机制

- 使用rsync确保源目录和目标目录完全一致，包括隐藏文件
- 在同步前会清空目标目录，避免残留已删除的文件
- 包含验证步骤，确认源目录和目标目录的文件数量一致

#### 触发条件

- 当推送到`master`分支，且符合上述规则的文件发生变化时
- 手动触发(通过GitHub Actions界面)

#### 使用前须知

1. 需要配置一个拥有目标仓库写入权限的Personal Access Token(PAT)
2. 在仓库的Settings > Secrets > Actions中添加名为`PAT`的secret

#### 如何配置PAT

1. 访问GitHub设置中的[Personal Access Tokens](https://github.com/settings/tokens)
2. 点击"Generate new token"
3. 选择"repo"权限范围
4. 生成并复制token
5. 在本仓库的Settings > Secrets > Actions中添加名为`PAT`的secret，值为刚才复制的token

#### 如何手动触发

1. 在GitHub仓库页面点击"Actions"标签
2. 在左侧列表中找到"同步UPM包到包仓库"
3. 点击"Run workflow"按钮，选择要运行的分支
4. 点击"Run workflow"确认运行 