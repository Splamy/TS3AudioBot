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
The bot is split up into 2 parts:

1. The Main-Bot:
  * is processing all your commands via TeamSpeak's serverquery.
  * Connects the ts3-audio-client if music should be played. (Support for more at once i planned)
  * [broken] is able to stream to different Channels and/or Users simultaneously with TeamSpeak's whisper feature.

## Features & Plannings
Done:
* Extract Youtube and Soundcloud songs as well as stream Twitch
* Extensive history manager, including features like:
  - getting the last x played songs
  - get last x songs played by a certain user
  - start any once played song again via id
  - search in title from played songs
  - (planned) combined search expressions
* [broken] (un)subscribe to the Bob to hear music in any channel
* [broken] (un)subscribe the Bob to certain channels
* Playlist management for all users
* [broken] Basic plugin support

In progress:
* -- nothing --

In planning:
* Create multiple client instances automatically for diffrent channels
* Rights system

## Existing commands
All in all, the bot is fully operable only via chat (and actually only via chat).  
Commands are invoked with !command.  
Some commands have restrictions, like they can only be used in a private chat, only in public chat, or need admin rights.

* *add*: Adds a new song to the queue.
* *clear*: Removes all songs from the current playlist.
* *getuserid*: Gets the unique Id of a user.
* *help*: Shows all commands or detailed help about a specific command.
* *history*: Shows recently played songs.
* *kickme*: Guess what?
* *link*: Gets a close to original link so you can open the original song in youtube, soundcloud, etc.
* *loop*: Sets whether of not to loop the entire playlist.
* *next*: Plays the next song in the playlist.
* *pm*: Requests private session with the ServerBot so you can invoke private commands.
* *play*: Automatically tries to decide whether the link is a special resource (like youtube) or a direct resource (like ./hello.mp3) and starts it.
* *previous*: Plays the previous song in the playlist.
* *quit*: Closes the TS3AudioBot application.
* *quiz*: Enable to hide the songnames and let your friends guess the title.
* *repeat*: Sets whether or not to loop a single song
* *seek*: Jumps to a timemark within the current song.
* *song*: Tells you the name of the current song.
* *soundcloud*: Resolves the link as a soundcloud song to play it for you.
* *subscribe*: Lets you hear the music independent from the channel you are in.
* *stop*: Stops the current song.
* *test*: Only for debugging purposes
* *twitch*: Resolves the link as a twitch stream to play it for you.
* *unsubscribe*: Only lets you hear the music in active channels again.
* *volume*: Sets the volume level of the music.
* *youtube*: Resolves the link as a youtube video to play it for you.  
This command will find every link containing something like ?v=Unique_TYID  
If the bot can't play a video it might be due to some embedding restrictions, which are blocking this.  
For now we don't have any workaround for that.

## How to set up the bot
### Dependencies
 * Any C# Compiler (Visual Studio or mono 4.0.0+ and xbuild)
 * A C Compiler for Opus

### Compilation
Download the git repository with `git clone https://github.com/Splamy/TS3AudioBot.git`.

#### Linux
1. Run the `InstallOpus.sh`
1. Get `NuGet.exe` (if you dont have it yet) with `wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe`
1. Go into the directory of the repository with `cd TS3AudioBot`
1. Execute `mono Nuget.exe restore` to download all dependencies
1. Execute `xbuild /p:Configuration=Release TS3AudioBot.sln` to build the C# AudioBot

#### Windows
Build the C# AudioBot with Visual Studio.

### Installation
1. Create 2 groups on the TeamSpeak server:
  * one for the ServerBot with enough rights so he can
    * join as a serverquery
    * view all server/channel/clients
    * write in all/private chat
    * optionally kick clients form channel/server
  * one for the AudioBotAdmin with no requirements (just ensure a high enough `i_group_needed_member_add_power`).  
1. The first time you'll need to run the TS3Audiobot.exe without parameter and
it will ask you a few questions. You can get ServerGroupIds in the rights window.
1. Now you can move the process to the backgroud or close the bot with `!quit` in teamspeak and run it in the background.  
The recommended start from now on is `TS3AudioBot.exe -q` to disable writing to stdout since the bot logs everything to a log file anyway.
1. Congratz, you're done! Enjoy listening to your favourite music, experimenting with the crazy command system or do whatever you whish to do ;).

### Testing and Fuzzying
1. Run the *TS3ABotUnitTests* project in Visual Studio or Monodevelop.