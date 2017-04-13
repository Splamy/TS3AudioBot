# TS3AudioBot
|master|develop|
|:--:|:--:|
|[![Build Status](https://travis-ci.org/Splamy/TS3AudioBot.svg?branch=master)](https://travis-ci.org/Splamy/TS3AudioBot)|[![Build Status](https://travis-ci.org/Splamy/TS3AudioBot.svg?branch=develop)](https://travis-ci.org/Splamy/TS3AudioBot)|

## About
This is our open-source TeamSpeak 3 audiobot project since
we haven't found any other open-source one so far.  
The bot has come a long way is pretty stable by now, though somtimes he hangs up or needs some other maintenance.  
For now I'd only recomment this bot on small servers since it doesn't cover any more complex right systems and relies on discipline.  

## How our Bot works
The TS3AudioBot connects with at least 1 TeamSpeak3 Client instance wich allows you to:
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
* *broken* | Basic plugin support

In progress:
* Web API

In planning:
* Create multiple client instances automatically for diffrent channels
* (Improved) Rights system
* Own web-interface

## Bot Commands
All in all, the bot is fully operable only via chat (and actually only via chat).  
Commands are invoked with !command.  
Some commands have restrictions, like they can only be used in a private chat, only in public chat, or need admin rights.

For the full command list and tutorials see [here in the wiki](https://github.com/Splamy/TS3AudioBot/wiki/CommandSystem)

If the bot can't play some youtube videos it might be due to some embedding restrictions, which are blocking this.  
You can add a [youtube-dl](https://github.com/rg3/youtube-dl/) binary or source folder and specify the path in the config to try to bypass this.

## How to set up the bot
### Dependencies
 * Any C# Compiler (Visual Studio or mono 4.0.0+ and xbuild)
 * (Linux only) A C Compiler for Opus

### Compilation
Before we start: _If you know what you are doing_ you can alternatively compile each depenency referenced here from source/git by yourself, but I won't add a tutorial for that.

Download the git repository with `git clone https://github.com/Splamy/TS3AudioBot.git`.

#### Linux
1. See if you have NuGet by just executing `nuget`. If not, get `NuGet.exe` with `wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe`
1. Go into the directory of the repository with `cd TS3AudioBot`
1. Execute `nuget restore` or `mono ../Nuget.exe restore` to download all dependencies
1. Execute `xbuild /p:Configuration=Release /p:Platform=x86 TS3AudioBot.sln` to build the C# AudioBot
1. Make the Opus script runnable with `chmod u+x InstallOpus.sh` and run it with `./InstallOpus.sh`
1. Get the ffmpeg [32bit](https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-32bit-static.tar.xz) or [64bit](https://johnvansickle.com/ffmpeg/builds/ffmpeg-git-64bit-static.tar.xz) binary.
1. Extract the ffmpeg archive with `tar -vxf ffmpeg-git-XXbit-static.tar.xz`
1. Get the ffmpeg binary from `ffmpeg-git-*DATE*-64bit-static\ffmpeg` and copy it to `TS3AudioBot/bin/Release/`

#### Windows
1. Build the C# AudioBot with Visual Studio.
1. Download the latest libopus file from [here](https://archive.mozilla.org/pub/opus/win32/opusfile-0.7-win32.zip)
1. Open the archive and copy the file from `opusfile-0.7-win32\libopus-0.dll`, move **and rename** it to `TS3AudioBot\bin\Release\libopus.dll`
1. Get the ffmpeg [32bit](https://ffmpeg.zeranoe.com/builds/win32/static/ffmpeg-latest-win32-static.zip) or [64bit](https://ffmpeg.zeranoe.com/builds/win64/static/ffmpeg-latest-win64-static.zip) binary.
1. Open the archive and copy the ffmpeg binary from `ffmpeg-latest-winXX-static\bin\ffmpeg.exe` to `TS3AudioBot\bin\Release\`

### Installation
1. Create 2 groups on the TeamSpeak server:
  * one for the ServerBot with enough rights so he can
    * join as a serverquery
    * view all server/channel/clients
    * write in all/private chat
    * optionally kick clients form channel/server
    * See [here](https://github.com/Splamy/TS3AudioBot/issues/29#issuecomment-233582408) for a more detailed list until I update the readme...
  * one for the AudioBotAdmin with no requirements (just ensure a high enough `i_group_needed_member_add_power`).  
1. The first time you'll need to run the TS3Audiobot.exe without parameter and
it will ask you a few questions. You can get ServerGroupIds in the rights window.
1. Now you can move the process to the backgroud or close the bot with `!quit` in teamspeak and run it in the background.  
The recommended start from now on is `TS3AudioBot.exe -q` to disable writing to stdout since the bot logs everything to a log file anyway.
1. Congratz, you're done! Enjoy listening to your favourite music, experimenting with the crazy command system or do whatever you whish to do ;).

### Testing and Fuzzying
1. Run the *TS3ABotUnitTests* project in Visual Studio or Monodevelop.
