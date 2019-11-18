# TS3AudioBot

This is a open-source TeamSpeak3 bot, playing music and much more.  

- **Got questions?** Check out our [Wiki](https://github.com/Splamy/TS3AudioBot/wiki), [FAQ](https://github.com/Splamy/TS3AudioBot/wiki/FAQ), or ask on our [![Join Gitter Chat](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/TS3AudioBot/Lobby?utm_source=share-link&utm_medium=link&utm_campaign=share-link)
- **Something's broken or it's complicated?** [Open an issue](https://github.com/Splamy/TS3AudioBot/issues/new/choose)
  - Please use and fill out one of the templates we provide unless they are not applicable or you have a good reason not to.  
    This helps us getting through the technical stuff faster
  - Please keep issues in english, this makes it easier for everyone to participate and keeps issues relevant to link to.
- **Want to support this Project?**
  - You can discuss and suggest features. However the [backlog](https://github.com/Splamy/TS3AudioBot/projects/2) is large and feature requests will probably take time
  - You can contribute code. This is always appreciated, please open an issue or contact a maintainer to discuss *before* you start.
  - You can support me on [![Patreon][patreon-badge]][patreon-link] or [![Paypal][paypal-badge]][paypal-link]

[patreon-badge]: https://img.shields.io/endpoint.svg?url=https%3A%2F%2Fshieldsio-patreon.herokuapp.com%2FSplamy&style=flat-square
[patreon-link]: https://patreon.com/Splamy

[paypal-badge]: https://img.shields.io/badge/Paypal-Donate!-%2300457C.svg?logo=paypal&style=flat-square
[paypal-link]: https://paypal.me/Splamy

## Features
* Play Youtube and Soundcloud songs as well as stream Twitch (extensible with plugins)
* Song history
* Various voice subscription modes; including to clients, channels and whisper groups
* Playlist management for all users
* Powerful permission configuration
* Plugin support
* Web API
* Multi-instance
* Localization
* Low CPU and memory with our self-written headless ts3 client

To see what's planned and in progress take a look into our [Roadmap](https://github.com/Splamy/TS3AudioBot/projects/2).

## Bot Commands
The bot is fully operable via chat.  
To get started write `!help` to the bot.  
For all commands check out our live [OpenApiV3 generator](http://tab.splamy.de/openapi/index.html).  
For an in-depth command tutorial see [here in the wiki](https://github.com/Splamy/TS3AudioBot/wiki/CommandSystem).

## Install

### Download
Download either one of the latest builds from our [nightly server](https://splamy.de/Nightly#ts3ab):  
- [![Download](https://img.shields.io/badge/Download-master-green.svg)](https://splamy.de/api/nightly/ts3ab/master_dotnet_core/download)
  Versions are mostly considered stable but won't get bigger features as fast.
- [![Download](https://img.shields.io/badge/Download-develop-green.svg)](https://splamy.de/api/nightly/ts3ab/develop_dotnet_core/download)
  Will always have the latest and greatest but might not be fully stable or have broken features.

#### Linux
1. dotnet core: Get the latest `dotnet core 2.2` version by following [this tutorial](https://dotnet.microsoft.com/download/linux-package-manager/ubuntu16-04/sdk-current) and follow the steps after choosing your platform
1. Other dependencies:
* on **Ubuntu**:  
Run `sudo apt-get install libopus-dev ffmpeg`
* on **Arch Linux**:  
Run `sudo pacman -S opus ffmpeg`
* on **CentOS 7**:  
Run
    ```
    sudo yum -y install epel-release
    sudo rpm -Uvh http://li.nux.ro/download/nux/dextop/el7/x86_64/nux-dextop-release-0-5.el7.nux.noarch.rpm
    sudo yum -y install ffmpeg opus-devel
	```
* **manually**:
    1. Make sure you have a C compiler installed
    1. Make the Opus script runnable with `chmod u+x InstallOpus.sh` and run it with `./InstallOpus.sh`
    1. Get the ffmpeg [32bit](https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-32bit-static.tar.xz) or [64bit](https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-64bit-static.tar.xz) binary.
    1. Extract the ffmpeg archive with `tar -vxf ffmpeg-git-XXbit-static.tar.xz`
    1. Get the ffmpeg binary from `ffmpeg-git-*DATE*-64bit-static/ffmpeg` and copy it to `TS3AudioBot/bin/Release/netcoreapp2.2`

#### Windows
1. Get the ffmpeg [32bit](https://ffmpeg.zeranoe.com/builds/win32/static/ffmpeg-latest-win32-static.zip) or [64bit](https://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-latest-win64-static.zip) binary.
1. Open the archive and copy the ffmpeg binary from `ffmpeg-latest-winXX-static/bin/ffmpeg.exe` to `TS3AudioBot/bin/Release/netcoreapp2.2`

### Optional Dependencies
If the bot can't play some youtube videos it might be due to some embedding restrictions which are blocking this.  
You can add a [youtube-dl](https://github.com/rg3/youtube-dl/) binary or source folder and specify the path in the config to try to bypass this.

### First time setup
1. Run the bot with `dotnet TS3AudioBot.dll` and follow the setup instructions.
1. (Optional) Close the bot and configure your `rights.toml` to your desires.
You can use the template rules as suggested in the automatically generated file,
or dive into the rights syntax [here](https://github.com/Splamy/TS3AudioBot/wiki/Rights).
Then start the bot again.
1. (Optional, but highly recommended for everything to work properly).
   - Create a privilege key for the ServerAdmin group (or a group which has equivalent rights).
   - Send the bot in a private message `!bot setup <privilege key>`.
1. Congratz, you're done! Enjoy listening to your favourite music, experimenting with the crazy command system or do whatever you whish to do ;).  
For further reading check out the [CommandSystem](https://github.com/Splamy/TS3AudioBot/wiki/CommandSystem).

## Building manually

|master|develop|
|:--:|:--:|
|[![Build status](https://ci.appveyor.com/api/projects/status/i7nrhqkbntdhwpxp/branch/master?svg=true)](https://ci.appveyor.com/project/Splamy/ts3audiobot/branch/master)|[![Build status](https://ci.appveyor.com/api/projects/status/i7nrhqkbntdhwpxp/branch/develop?svg=true)](https://ci.appveyor.com/project/Splamy/ts3audiobot/branch/develop)|

### Download
Download the git repository with `git clone --recurse-submodules https://github.com/Splamy/TS3AudioBot.git`.

#### Linux
1. Get the latest `dotnet core 2.2` version by following [this tutorial](https://dotnet.microsoft.com/download/linux-package-manager/ubuntu16-04/sdk-current) and choose your platform
1. Go into the directory of the repository with `cd TS3AudioBot`
1. Execute `dotnet build --framework netcoreapp2.2 --configuration Release TS3AudioBot` to build the AudioBot
1. The binary will be in `./TS3AudioBot/bin/Release/netcoreapp2.2` and can be run with `dotnet TS3AudioBot.dll`

#### Windows
1. Make sure you have installed `Visual Studio` with `.NET Framework 4.7.2` and the latest `dotnet core 2.2` or higher
1. Build the AudioBot with Visual Studio.

## Community

### Localization
:speech_balloon: *Want to help translate or improve translation?*  
Join us on [Transifex](https://www.transifex.com/respeak/ts3audiobot/) to help translate  
or in our [Gitter](https://gitter.im/TS3AudioBot/Lobby?utm_source=share-link&utm_medium=link&utm_campaign=share-link) to discuss or ask anything!  
All help is appreciated :heart:

Translations need to be manually approved and will then be automatically built and deployed to [our nightly server here](https://splamy.de/TS3AudioBot).

## License
This project is licensed under [OSL-3.0](https://opensource.org/licenses/OSL-3.0).

Why OSL-3.0:
- OSL allows you to link to our libraries without needing to disclose your own project, which might be useful if you want to use the TSLib as a library.
- If you create plugins you do not have to make them public like in GPL. (Although we would be happy if you shared them :)
- With OSL we want to allow you providing the TS3AB as a service (even commercially). We do not want the software to be sold but the service. We want this software to be free for everyone.
- TL; DR? https://tldrlegal.com/license/open-software-licence-3.0

---
[![forthebadge](http://forthebadge.com/images/badges/60-percent-of-the-time-works-every-time.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/built-by-developers.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/built-with-love.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/contains-cat-gifs.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/made-with-c-sharp.svg)](http://forthebadge.com)
