# TS3AudioBot
## About
This is our open-source Teamspeak 3 audiobot project since
we haven't found any other open-source one so far.  
The bot has come a long way is pretty stable by now, though somtimes he hangs up or needs some other maintenance.  
For now i'd only recomment this bot on small servers since it doesn't cover any more complex right systems and relies on discipline.  
And last but not least: the majority of the magic is happening in the C# part, so if you want to contribute
don't be scared about the 50%+ C/C++ rate :).

## How our Bot works
The bot is split up into 3 parts:

1. The Main-Bot:
  * is processing all your commands via TeamSpeak's serverquery.
  * starts the "SeverBob" if music should be played.
2. The ServerBob:
  * is a plain TeamSpeak3 client with a custom plugin.
  * he is able to stream to different Channels and/or Users simultaneously with TeamSpeak's whisper feature
3. And VLC:
  * simply because it's probably the best mediaplayer in existence. 
  * (and we couldn't get anything else working properly, soo...)

## Existing commands
All in all, the bot is fully operatable only via chat (and actually only via chat)  
Commands are invoked with !command  
Some commands have restrictions, like they can only be used in a private chat, only in public chat, or need admin rights.

* *add*: Adds a new song to the queue.
* *clear*: Removes all songs from the current playlist.
* *getuserid*: Gets the unique Id of a user.
* *help*: Shows all commands or detailed help about a specific command.
* *history*: Shows recently played songs.
* *kickme*: Guess what?
* *link*: Plays any direct resource link.
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
* *unsubscribe*: Only lets you hear the music in active channels again.
* *volume*: Sets the volume level of the music.
* *youtube*: Resolves the link as a youtube video to play it for you.  
This command will find every link containing something like ?v=Unique_TYID  
If the bot can't play a video it might be due to some embedding restrictions, which are blocking this.  
For now we don't have any workaround for that.

## How to set the bot up (uncomplete tutorial!)
1. Download the latest TS3AB + TS3Client Plugin  
(or compile it by yourself, you'll need a C# compiler like VS or mono + xbuild
  as well as scons and a C++ compiler)
1. Linux: you'll either need a X environment capable of running window applications or install
a virtual X interface like Xvfb.
Our Xvfb one time start looks like this: `Xvfb :1 -screen 0 640x480x24 > /dev/null 2>&1 & disown`  
Windows: Ignore this step.
1. Linux: Put a StartTsBot.sh executing "export DISPLAY=:1" and starting the ts3client to the TS3Audiobot.exe  
Windows: not yet working.
1. Setup the audio redirecting from vlc to the client:  
Linux, something like:
	```
	#!/usr/bin/env bash
	set -e
	
	pactl load-module module-null-sink sink_name=defaultSink
	id=`pactl list source-outputs | grep TeamSpeak3 -B25 | grep "Source Output" | cut -d# -f2`
	pactl move-source-output $id defaultSink.monitor
	
	id=`pactl list sink-inputs | grep TeamSpeak3 -B25 | grep "Sink Input" | cut -d# -f2`
	pactl set-sink-input-volume $id 0
	```
Windows:  
	Configure the Stereomix to get the vlc output to the ts3client. 
1. Create 2 groups:
  * one for the ServerBob with enough rights so he can
    * join as a serverquery
	* view all server/channel/clients
	* write in all/private chat
	* optionally kick clients form channel/server
  * one for the AudioBotAdmin with no requirements (just ensure a high enough i_group_needed_member_add_power)
1. The first time you'll need to start the TS3Audiobot.exe without parameter and
it will ask you a few questions. You can get ServerGroupIds in the rights window.
1. Now you can close the bot with "quit" and run it with TS3AudioBot.exe -I -S from now on. (add a & on linux to start in background)