# TS3AudioBot
|master|develop|
|:--:|:--:|
|[![Build Status](https://travis-ci.org/Splamy/TS3AudioBot.svg?branch=master)](https://travis-ci.org/Splamy/TS3AudioBot)|[![Build Status](https://travis-ci.org/Splamy/TS3AudioBot.svg?branch=develop)](https://travis-ci.org/Splamy/TS3AudioBot)|

Nightly builds are available here: http://splamy.de/Nightly#ts3ab

## About
This is our open-source TeamSpeak 3 audiobot project since
we haven't found any other open-source one so far.  
The bot has come a long way is pretty stable by now, though sometimes he hangs up or needs some other maintenance.  
For now I'd only recommend this bot on small servers since it doesn't cover any more complex right systems and relies on discipline.  

## How our Bot works
The TS3AudioBot connects with at least 1 TeamSpeak3 Client instance which allows you to:
  * issue commands to that instance.
  * play music for your channel.
  * tell him to stream to different Channels and/or Users simultaneously with TeamSpeak's whisper feature.

We use a self written TeamSpeak3 Client which gives us very low memory and cpu usage.  
About _65MB_ Ram with 1700+ songs in history indexed  
And _4-6% CPU_ usage on a single shared vCore from a _Intel Xeon E5-1650 v2 @ 3.50GHz_  

## Features & Plannings
Done:
* Extract Youtube and Soundcloud songs as well as stream Twitch
* Extensive history manager, including features like:
  - getting the last x played songs
  - get last x songs played by a certain user
  - start any once played song again via id
  - search in title from played songs
  - (planned) combined search expressions
* (un)subscribe to the Bob to hear music in any channel
* (un)subscribe the Bob to certain channels
* Playlist management for all users
* Advanced permission configuration
* Extensive plugin support
* Web API

In progress:
* Own web-interface
* (Improved) Rights system
* Multi-instance

In planning:  
*See issues*

## Bot Commands
All in all, the bot is fully operable only via chat (and actually only via chat).  
Commands are invoked with !command.  
Some commands have restrictions, like they can only be used in a private chat, only in public chat, or need admin rights.

For the full command list and tutorials see [here in the wiki](https://github.com/Splamy/TS3AudioBot/wiki/CommandSystem)

If the bot can't play some youtube videos it might be due to some embedding restrictions, which are blocking this.  
You can add a [youtube-dl](https://github.com/rg3/youtube-dl/) binary or source folder and specify the path in the config to try to bypass this.

## How to set up the bot

If you dont want to compile the AudioBot yourself you can always download the
latest version from our nightly server (linked at the top) and jump straight to
"Getting the dependencies" for your platform.

### Prerequisites
* Any C# Compiler (`Visual Studio` with `.NET 4.6` or `mono 5.10.0+` with `msbuild`)
* (Linux only) A C Compiler for Opus

### Compilation
Before we start: _If you know what you are doing_ you can alternatively compile each dependency referenced here from source/git by yourself, but I won't add a tutorial for that.

Download the git repository with `git clone --recurse-submodules https://github.com/Splamy/TS3AudioBot.git`.

#### Linux
1. Get the latest mono version by following [this tutorial](http://www.mono-project.com/download/stable/#download-lin) and install `mono-devel`
1. See if you have NuGet by just executing `nuget`. If not, get `nuget.exe` with `wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe`
1. Go into the directory of the repository with `cd TS3AudioBot`
1. Execute `nuget restore` or `mono ../nuget.exe restore` to download all dependencies
1. Execute `msbuild /p:Configuration=Release TS3AudioBot.sln` to build the AudioBot
1. Getting the dependencies
    * on **Ubuntu**:  
    Run `sudo apt-get install libopus-dev ffmpeg`
    * on **Arch Linux**:  
    Run `sudo pacman -S opus ffmpeg`
    * **manually**:
        1. Make the Opus script runnable with `chmod u+x InstallOpus.sh` and run it with `./InstallOpus.sh`
        1. Get the ffmpeg [32bit](https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-32bit-static.tar.xz) or [64bit](https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-64bit-static.tar.xz) binary.
        1. Extract the ffmpeg archive with `tar -vxf ffmpeg-git-XXbit-static.tar.xz`
        1. Get the ffmpeg binary from `ffmpeg-git-*DATE*-64bit-static\ffmpeg` and copy it to `TS3AudioBot/bin/Release/`

#### Windows
1. Make sure you have installed `.NET Framework 4.6`
1. Build the AudioBot with Visual Studio.
1. Getting the dependencies
   1. Get the ffmpeg [32bit](https://ffmpeg.zeranoe.com/builds/win32/static/ffmpeg-latest-win32-static.zip) or [64bit](https://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-latest-win64-static.zip) binary.
   1. Open the archive and copy the ffmpeg binary from `ffmpeg-latest-winXX-static\bin\ffmpeg.exe` to `TS3AudioBot\bin\Release\`

### Installation
1. Create a group for the AudioBotAdmin with no requirements (just ensure a high enough `i_group_needed_member_add_power`).
1. Create a privilege key for the ServerAdmin group (or a group which has equivalent rights).
1. The first time you'll need to run `mono TS3Audiobot.exe` without parameter and
it will ask you a few questions.
1. Close the bot again and configure your `rights.toml` in `TS3AudioBot\bin\Release\` to your desires.
You can use the template rules and assign your admin as suggested in the automatically generated file,
or dive into the Rights syntax [here](https://github.com/Splamy/TS3AudioBot/wiki/Rights).
1. Start the bot again.
1. Send the bot in a private message `!bot setup <key>` where `<key>` is the privilege key from a previous step.
1. Now you can move the process to the background or close the bot with `!quit` in teamspeak and run it in the background.  
1. (optional) You can configure the logging levels and outputs in the `NLog.config` file, read [here](https://github.com/NLog/NLog/wiki/Configuration-file) to learn more.
1. Congratz, you're done! Enjoy listening to your favourite music, experimenting with the crazy command system or do whatever you whish to do ;).  
For further reading check out the [CommandSystem](https://github.com/Splamy/TS3AudioBot/wiki/CommandSystem)

### Testing and Fuzzying
1. Run the *TS3ABotUnitTests* project in Visual Studio or Monodevelop.

# License
This project is licensed under OSL-3.0.

Why OSL-3.0:
- OSL allows you to link to our libraries without needing to disclose your own project, which might be useful if you want to use the TS3Client as a library.
- If you create plugins you do not have to make them public like in GPL. (Although we would be happier if you shared them :)
- With OSL we want to allow you providing the TS3AB as a service (even commercially). We do not want the software to be sold but the service. We want this software to be free for everyone.

# Badges
[![forthebadge](http://forthebadge.com/images/badges/60-percent-of-the-time-works-every-time.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/built-by-developers.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/built-with-love.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/contains-cat-gifs.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/made-with-c-sharp.svg)](http://forthebadge.com)
