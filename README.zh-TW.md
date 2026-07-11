# TS3AudioBot

[English](README.md) · [简体中文](README.zh-CN.md) · 繁體中文

這是一個開源的 TeamSpeak3 機器人，可以播放音樂以及更多功能。

- **有問題？** 查看我們的 [Wiki](https://github.com/Splamy/TS3AudioBot/wiki)、[FAQ](https://github.com/Splamy/TS3AudioBot/wiki/FAQ)，或在 [![加入 Gitter 聊天](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/TS3AudioBot/Lobby?utm_source=share-link&utm_medium=link&utm_campaign=share-link) 上發問
- **遇到故障或複雜問題？** [提交 Issue](https://github.com/Splamy/TS3AudioBot/issues/new/choose)
  - 請使用並填寫我們提供的範本，除非範本不適用於你的情況或有充分理由不這麼做。
    這有助於我們更快地處理技術問題
  - 請盡量用英文提交 Issue，這樣便於大家參與，也便於相關聯的 Issue 能被正確引用。
- **想支援此專案？**
  - 你可以參與討論並提出功能建議。不過我們的[待辦清單](https://github.com/Splamy/TS3AudioBot/projects/2)很長，功能請求可能需要一些時間
  - 你可以貢獻程式碼。我們對此非常感激，請在**開始之前**提交 Issue 或聯絡維護者進行討論。
  - 你可以透過 [![Patreon][patreon-badge]][patreon-link] 或 [![Paypal][paypal-badge]][paypal-link] 支援我

[patreon-badge]: https://img.shields.io/badge/Patreon-Donate!-F96854.svg?logo=patreon&style=flat-square
[patreon-link]: https://patreon.com/Splamy

[paypal-badge]: https://img.shields.io/badge/Paypal-Donate!-00457C.svg?logo=paypal&style=flat-square
[paypal-link]: https://paypal.me/Splamy

## 特性

* 播放 YouTube 和 SoundCloud 歌曲，以及串流播放 Twitch（可透過外掛程式擴充）
* 歌曲歷史記錄
* 多種語音訂閱模式，包括：關注客戶端、頻道和耳語群組
* 面向所有使用者的播放清單管理
* 強大的權限設定
* 外掛程式支援
* Web API
* 多實例
* 在地化
* 憑藉自研無頭 TS3 用戶端，CPU 和記憶體佔用低

想瞭解計劃中的和正在進行的功能，請查看我們的[路線圖](https://github.com/Splamy/TS3AudioBot/projects/2)。

## 機器人指令

機器人完全可透過聊天進行操作。
開始使用請向機器人傳送 `!help`。
如需查看所有指令，請查閱我們的線上 [OpenApiV3 產生器](http://tab.splamy.de/openapi/index.html)。
深入的指令教學請參見[此處的 Wiki](https://github.com/Splamy/TS3AudioBot/wiki/CommandSystem)。

## 安裝

### 下載

根據你的平臺和偏好選擇並下載建置版本：

|  | 穩定版 | 實驗版 |
| -- | -- | -- |
| | 版本通常被認為是穩定的，但重大功能更新不會很快。 | 始終擁有最新最好的功能，但可能不完全穩定或存在有問題的功能。 |
| Windows_x64 | [![下載](https://img.shields.io/badge/Download-master-green.svg)](https://splamy.de/api/nightly/ts3ab/master_win_x64/download) | [![下載](https://img.shields.io/badge/Download-develop-green.svg)](https://splamy.de/api/nightly/ts3ab/develop_win_x64/download) |
| Linux_x64 | [![下載](https://img.shields.io/badge/Download-master-green.svg)](https://splamy.de/api/nightly/ts3ab/master_linux_x64/download) | [![下載](https://img.shields.io/badge/Download-develop-green.svg)](https://splamy.de/api/nightly/ts3ab/develop_linux_x64/download) |
| Docker | [![Docker](https://img.shields.io/badge/Docker-0.11.0-0db7ed.svg)](https://github.com/getdrunkonmovies-com/TS3AudioBot_docker)（注意：此建置版本由社群維護。它包含了所有相依套件以及預先設定好的 youtube-dl） | - |

（我們還有更多建置版本，如 linux arm/arm64 和依賴 .NET framework 的版本，可在我們的 [nightly 伺服器](https://splamy.de/Nightly#ts3ab) 上取得）

#### Linux

安裝所需的相依套件：

* **Ubuntu**/**Debian**：
  執行 `sudo apt-get install libopus-dev ffmpeg`
* **Arch Linux**：
  執行 `sudo pacman -S opus ffmpeg`
* **CentOS 7**：
  執行
  ```
  sudo yum -y install epel-release
  sudo rpm -Uvh http://li.nux.ro/download/nux/dextop/el7/x86_64/nux-dextop-release-0-5.el7.nux.noarch.rpm
  sudo yum -y install ffmpeg opus-devel
  ```
* **手動安裝**：
  1. 確保已安裝 C 編譯器
  2. 賦予 Opus 腳本可執行權限：`chmod u+x InstallOpus.sh`，然後執行 `./InstallOpus.sh`
  3. 取得 ffmpeg [32位元](https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-i686-static.tar.xz) 或 [64位元](https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-amd64-static.tar.xz) 二進位檔案
  4. 解壓縮 ffmpeg 壓縮包：`tar -vxf ffmpeg-git-*XXbit*-static.tar.xz`
  5. 從 `ffmpeg-git-*DATE*-amd64-static/ffmpeg` 取得 ffmpeg 二進位檔案，並複製到你的 TS3AudioBot 資料夾中

#### Windows

1. 取得 ffmpeg [32位元](https://ffmpeg.zeranoe.com/builds/win32/static/ffmpeg-latest-win32-static.zip) 或 [64位元](https://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-latest-win64-static.zip) 二進位檔案
2. 開啟壓縮包，將 `ffmpeg-latest-winXX-static/bin/ffmpeg.exe` 中的 ffmpeg 二進位檔案複製到你的 TS3AudioBot 資料夾中

### 選用相依套件

如果機器人無法播放某些 YouTube 影片，可能是因為嵌入限制導致的。
你可以安裝 [youtube-dl](https://github.com/rg3/youtube-dl/) 的二進位檔案或原始碼資料夾（並在設定中指定路徑），以嘗試繞過此限制。

### 首次設定

1. 執行機器人：`./TS3AudioBot`（Linux）或 `TS3AudioBot.exe`（Windows），然後按照設定指示操作。
2. （選用）關閉機器人，根據需要設定 `rights.toml` 檔案。
   你可以使用自動生成檔案中建議的範本規則，
   或深入瞭解[這裡的權限語法](https://github.com/Splamy/TS3AudioBot/wiki/Rights)。
   然後重新啟動機器人。
3. （選用，但強烈建議以確保一切正常執行）。
   - 為 ServerAdmin 群組（或具有等效權限的群組）建立一個權限金鑰。
   - 向機器人傳送私訊 `!bot setup <權限金鑰>`。
4. 恭喜，你已經完成了！盡情享受你最喜愛的音樂，體驗這個強大的指令系統，或者隨心所欲地探索吧 ;)。
   更多資訊請查閱 [CommandSystem](https://github.com/Splamy/TS3AudioBot/wiki/CommandSystem)。

## 手動建置

| master | develop |
|:--:|:--:|
| [![建置狀態](https://ci.appveyor.com/api/projects/status/i7nrhqkbntdhwpxp/branch/master?svg=true)](https://ci.appveyor.com/project/Splamy/ts3audiobot/branch/master) | [![建置狀態](https://ci.appveyor.com/api/projects/status/i7nrhqkbntdhwpxp/branch/develop?svg=true)](https://ci.appveyor.com/project/Splamy/ts3audiobot/branch/develop) |

### 下載原始碼

使用 `git clone --recurse-submodules https://github.com/Splamy/TS3AudioBot.git` 克隆程式碼倉庫。

#### Linux

1. 按照[此教學](https://docs.microsoft.com/dotnet/core/install/linux-package-managers)取得最新的 `dotnet core 3.1` 版本，並選擇你的平臺
2. 進入倉庫目錄：`cd TS3AudioBot`
3. 執行 `dotnet build --framework netcoreapp3.1 --configuration Release TS3AudioBot` 建置 AudioBot
4. 產生的二進位檔案位於 `./TS3AudioBot/bin/Release/netcoreapp3.1`，可以透過 `dotnet TS3AudioBot.dll` 執行

#### Windows

1. 確保已安裝包含 `dotnet core 3.1` 開發工具鏈的 `Visual Studio`
2. 使用 Visual Studio 建置 AudioBot

### 建置 Web 介面

1. 開啟終端機，進入 `./WebInterface` 資料夾
2. 執行 `npm install` 還原或更新此專案的所有相依套件
3. 執行 `npm run build` 建置專案。
   建置後的專案位於 `./WebInterface/dist`。
   請確保在 ts3audiobot.toml 中將 webinterface 路徑設定為此資料夾。
4. 你也可以使用 `npm run start` 進行開發。
   這將使用 webpack 開發伺服器並支援熱重載，而非使用 ts3ab 自帶的伺服器。

## 社群

### 在地化

:speech_balloon: *想要協助翻譯或改善翻譯？*
加入我們的 [Transifex](https://www.transifex.com/respeak/ts3audiobot/) 協助翻譯，
或加入我們的 [Gitter](https://gitter.im/TS3AudioBot/Lobby?utm_source=share-link&utm_medium=link&utm_campaign=share-link) 討論或提問！
所有幫助都值得感謝 :heart:

翻譯需要經過人工審核，然後將自動建置並部署到[我們的 nightly 伺服器](https://splamy.de/TS3AudioBot)。

## 授權條款

本專案採用 [OSL-3.0](https://opensource.org/licenses/OSL-3.0) 授權。

為什麼選擇 OSL-3.0：

- OSL 允許你在連結到我們的函式庫時無需公開自己的專案，這對於你想將 TSLib 作為函式庫使用的情況可能很有用。
- 如果你建立外掛程式，不必像 GPL 那樣公開它們。（不過我們仍然希望你能分享它們 :)
- 透過 OSL，我們希望允許你將 TS3AB 作為服務提供（甚至是商業服務）。我們不希望本軟體被販售，而是希望服務被提供。我们希望這款軟體對所有人都是免費的。
- TL; DR? https://tldrlegal.com/license/open-software-licence-3.0

---
[![forthebadge](http://forthebadge.com/images/badges/60-percent-of-the-time-works-every-time.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/built-by-developers.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/built-with-love.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/contains-cat-gifs.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/made-with-c-sharp.svg)](http://forthebadge.com)
