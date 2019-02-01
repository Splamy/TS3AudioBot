# TS3AudioBot

|master|develop|Questions/Discussions|License|Support me|
|:--:|:--:|:--:|:--:|:--:|
|[![Build Status](https://travis-ci.org/Splamy/TS3AudioBot.svg?branch=master)](https://travis-ci.org/Splamy/TS3AudioBot)|[![Build Status](https://travis-ci.org/Splamy/TS3AudioBot.svg?branch=develop)](https://travis-ci.org/Splamy/TS3AudioBot)|[![Join Gitter Chat](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/TS3AudioBot/Lobby?utm_source=share-link&utm_medium=link&utm_campaign=share-link)|[![License: OSL-3.0](https://img.shields.io/badge/License-OSL%203.0-blue.svg)](https://opensource.org/licenses/OSL-3.0)|[![Patreon](https://img.shields.io/badge/Patreon-Become%20a%20Patron-F96854.svg)](https://www.patreon.com/bePatron?u=11604963)|

## About
This is our open-source TeamSpeak 3 audio bot project since
we haven't found any other open-source one so far.  
The bot has come a long way is pretty stable by now, though sometimes he hangs up or needs some other maintenance.  

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
Commands can be invoked with `!command`.  

For the full command list and tutorials see [here in the wiki](https://github.com/Splamy/TS3AudioBot/wiki/CommandSystem)
or our live [OpenApiV3 generator](http://tab.splamy.de/openapi/index.html).

## Download
You can download the latest builds precompiled from our [nightly server](https://splamy.de/Nightly#ts3ab):  
- [![Download](https://img.shields.io/badge/Download-master-green.svg)](https://splamy.de/api/nightly/ts3ab/master/download)
  versions are mostly considered stable but won't get bigger features as fast.
- [![Download](https://img.shields.io/badge/Download-develop-green.svg)](https://splamy.de/api/nightly/ts3ab/develop/download)
  will always have the latest and greatest but might not be fully stable or have broken features.

Continue with downloading the dependencies.

### Dependencies
You will need to download a few things for the bot to run:

#### Linux
1. Mono: Get the latest version by following [this tutorial](https://www.mono-project.com/download/stable/#download-lin) and install `mono-complete` or `mono-devel`
1. Other dependencies:
* on **Ubuntu**:  
Run `sudo apt-get install libopus-dev ffmpeg`
* on **Arch Linux**:  
Run `sudo pacman -S opus ffmpeg`
* on **CentOS 7**:
Run ```sudo yum -y install epel-release
    sudo rpm -Uvh http://li.nux.ro/download/nux/dextop/el7/x86_64/nux-dextop-release-0-5.el7.nux.noarch.rpm
    sudo yum -y install ffmpeg opus-devel```
* **manually**:
    1. Make sure you have a C compiler installed
    1. Make the Opus script runnable with `chmod u+x InstallOpus.sh` and run it with `./InstallOpus.sh`
    1. Get the ffmpeg [32bit](https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-32bit-static.tar.xz) or [64bit](https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-64bit-static.tar.xz) binary.
    1. Extract the ffmpeg archive with `tar -vxf ffmpeg-git-XXbit-static.tar.xz`
    1. Get the ffmpeg binary from `ffmpeg-git-*DATE*-64bit-static\ffmpeg` and copy it to `TS3AudioBot/bin/Release/`

#### Windows
1. Get the ffmpeg [32bit](https://ffmpeg.zeranoe.com/builds/win32/static/ffmpeg-latest-win32-static.zip) or [64bit](https://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-latest-win64-static.zip) binary.
1. Open the archive and copy the ffmpeg binary from `ffmpeg-latest-winXX-static\bin\ffmpeg.exe` to `TS3AudioBot\bin\Release\net46`

### Optional Dependencies
If the bot can't play some youtube videos it might be due to some embedding restrictions which are blocking this.  
You can add a [youtube-dl](https://github.com/rg3/youtube-dl/) binary or source folder and specify the path in the config to try to bypass this.

## Suggested first time setup
1. The first time you'll need to run `mono TS3AudioBot.exe` without parameter and
it will ask you a few questions.
1. Close the bot again and configure your `rights.toml` to your desires.
You can use the template rules and assign your admin as suggested in the automatically generated file,
or dive into the rights syntax [here](https://github.com/Splamy/TS3AudioBot/wiki/Rights).
1. Start the bot again.
1. This step is optional but highly recommended for everything to work properly.
   - Create a privilege key for the ServerAdmin group (or a group which has equivalent rights).
   - Send the bot in a private message `!bot setup <privilege key>`.
1. Congratz, you're done! Enjoy listening to your favourite music, experimenting with the crazy command system or do whatever you whish to do ;).  
For further reading check out the [CommandSystem](https://github.com/Splamy/TS3AudioBot/wiki/CommandSystem).

## Building manually

### Download
Download the git repository with `git clone --recurse-submodules https://github.com/Splamy/TS3AudioBot.git`.

#### Linux
1. Get the latest mono version by following [this tutorial](https://www.mono-project.com/download/stable/#download-lin) and install `mono-devel`
1. See if you have NuGet by just executing `nuget`.
   If not, get it with `sudo apt install nuget msbuild` (or the packet manager or your distribution),
   or manually with `wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe`
1. Go into the directory of the repository with `cd TS3AudioBot`
1. Execute `nuget restore` or `mono ../nuget.exe restore` to download all dependencies
1. Execute `msbuild /p:Configuration=Release /p:TargetFramework=net46 TS3AudioBot.sln` to build the AudioBot

#### Windows
1. Make sure you have installed `Visual Studio` and `.NET Framework 4.6` and the latest `dotnet core`
1. Build the AudioBot with Visual Studio.

### Testing and Fuzzing
1. Run the *TS3ABotUnitTests* project in Visual Studio or Monodevelop.

## Community

### Localization
:speech_balloon: *Want to help translate or improve translation?*  
Join us on [Transifex](https://www.transifex.com/respeak/ts3audiobot/) to help translate  
or in our [Gitter](https://gitter.im/TS3AudioBot/Lobby?utm_source=share-link&utm_medium=link&utm_campaign=share-link) to discuss or ask anything!  
All help is appreciated :heart:

Translations need to be manually approved and will then be automatically built and deployed to [our nightly server here](https://splamy.de/TS3AudioBot).

## License
This project is licensed under OSL-3.0.

Why OSL-3.0:
- OSL allows you to link to our libraries without needing to disclose your own project, which might be useful if you want to use the TS3Client as a library.
- If you create plugins you do not have to make them public like in GPL. (Although we would be happier if you shared them :)
- With OSL we want to allow you providing the TS3AB as a service (even commercially). We do not want the software to be sold but the service. We want this software to be free for everyone.
- TL; DR? https://tldrlegal.com/license/open-software-licence-3.0

---
[![forthebadge](http://forthebadge.com/images/badges/60-percent-of-the-time-works-every-time.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/built-by-developers.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/built-with-love.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/contains-cat-gifs.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/made-with-c-sharp.svg)](http://forthebadge.com)
