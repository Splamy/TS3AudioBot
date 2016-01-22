#ifndef AUDIO_PACKET_READER_HPP
#define AUDIO_PACKET_READER_HPP

namespace audio
{
class Player;

class PacketReader
{
private:
	/** The reference to the player. */
	Player *player;

public:
	PacketReader(Player *player);
	~PacketReader();

	void read();

private:
	bool openStreamComponent(int streamId);
};
}

#endif
