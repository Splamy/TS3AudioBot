TS3 PROTOCOL PAPER
==================

# 0. Naming Conventions
- `(Client -> Server)` denotes packets from client to server.
- `(Client <- Server)` denotes packets from server to client.
- All datatypes are sent in network order (Big Endian) unless otherwise
specified.
- Datatypes are declared with a prefixing `u` or `i` for unsigned and signed
and a number for the bitlength.  
For example `u8` would be be the C equivalent of `uint8` or `unsigned char`
- Arrays are represented by the underlying datatype in square brackets,
additionally if the length is known it is added in the brackets, separated by
a semicolon. Eg: `[u8]`, `[i32; 16]`
- Array ranges (parts of an array) are specified in square brackets with the
included lower bound, a minus and the excluded upper bound. Eg: `[0-10]`


# 1. Low-Level Packets
## 1.1 Packet structure
- The packets are build in a fixed scheme,
though have differences depending in which direction.
- Every column here represents 1 byte.
- The entire packet size must be at max 500 bytes.

### 1.1.1 (Client -> Server)
    +--+--+--+--+--+--+--+--+--+--+--+--+--+---------//----------+
    |          MAC          | PId | CId |PT|        Data         |
    +--+--+--+--+--+--+--+--+--+--+--+--+--+---------//----------+
    |                       \     Meta     |
    \                Header                /

| Name | Size        | Datatype | Explanation                     |
|------|-------------|----------|---------------------------------|
| MAC  | 8 bytes     | [u8]     | EAX Message Authentication Code |
| PId  | 2 bytes     | u16      | Packet Id                       |
| CId  | 2 bytes     | u16      | Client Id                       |
| PT   | 1 byte      | u8       | Packet Type + Flags             |
| Data | <=487 bytes | [u8]     | The packet payload              |

### 1.1.2 (Client <- Server)
    +--+--+--+--+--+--+--+--+--+--+--+------------//-------------+
    |          MAC          | PId |PT|           Data            |
    +--+--+--+--+--+--+--+--+--+--+--+------------//-------------+
    |                       \  Meta  |
    \             Header             /

| Name | Size        | Datatype | Explanation                     |
|------|-------------|----------|---------------------------------|
| MAC  | 8 bytes     | [u8]     | EAX Message Authentication Code |
| PId  | 2 bytes     | u16      | Packet Id                       |
| PT   | 1 byte      | u8       | Packet Type + Flags             |
| Data | <=489 bytes | [u8]     | The packet payload              |

## 1.2 Packet Types
- `0x00` Voice
- `0x01` VoiceWhisper
- `0x02` Command
- `0x03` CommandLow
- `0x04` Ping
- `0x05` Pong
- `0x06` Ack
- `0x07` AckLow
- `0x08` Init1

## 1.3 Packet Type + Flags byte
The final byte then looks like this

    MSB                   LSB
    +--+--+--+--+--+--+--+--+
    |UE|CP|NP|FR|   Type    |
    +--+--+--+--+--+--+--+--+

| Name | Size  | Hex  | Explanation     |
|------|-------|------|-----------------|
| UE   | 1 bit | 0x80 | Unencrypted     |
| CP   | 1 bit | 0x40 | Compressed      |
| NP   | 1 bit | 0x20 | Newprotocol     |
| FR   | 1 bit | 0x10 | Fragmented      |
| Type | 4 bit | 0-8  | The packet type |

## 1.4 Packet Compression
To reduce a packet size the data can be compressed.
When the data is compressed the `Compressed` flag must be set.
The algorithm "QuickLZ" is used for compression.
QuickLZ offers different compression levels.
The chosen level differs depending on the packet direction as following
- (Client -> Server) Level 1
- (Client <- Server) Level 3

## 1.5 Packet Splitting
When the packet payload exceeds the maximum datablock size the data can be
split up across multiple packets.
When splitting occurs, the `Fragmented` flag must be set on the first and
the last packet. Other flags, if set, are only set on the first packet.
The data can additionally be compressed before splitting.

## 1.6 Packet Encryption
When a packet is not encrypted the `Unencrypted` flag is set. For encrypted
packets the flag gets cleared.
Packets get encrypted with EAX mode (AES-CTR with OMAC).
The en/decryption parameters get generated for each packet as follows

### 1.6.1 Inputs
| Name | Type     | Explanation                     |
|------|----------|---------------------------------|
| PT   | u8       | Packet Type                     |
| PId  | u16      | Packet Id                       |
| PGId | u32      | Packet GenerationId (see 1.9.2) |
| PD   | bool     | Packet Direction                |
| SIV  | [u8; 20] | Shared IV (see 3.2)             |

### 1.6.2 Generation pseudocode
    let temporary: [u8; 26]
    temporary[0]    = 0x30 if (Client <- Server)
                      0x31 if (Client -> Server)
    temporary[1]    = PT
    temporary[2-6]  = (PGId in network order)[0-4]
    temporary[6-26] = SIV[0-20]

    let keynonce: [u8; 32]
    keynonce        = SHA256(temporary)

    key: [u8; 16]   = keynonce[0-16]
    nonce: [u8; 16] = keynonce[16-32]
    key[0]          = key[0] xor ((PId & 0xFF00) >> 8)
    key[1]          = key[1] xor ((PId & 0x00FF) >> 0)

### 1.6.3 Encryption
The data can now be encrypted with the `key` and `nonce` from (see 1.6.2) as the
EAX key and nonce and the packet `Meta` as defined in (see 1.1) as the EAX
header (sometimes called "Associated Text"). The resulting EAX mac (sometimes
called "Tag") will be stored in the `MAC` field as defined in (see 2.1).

### 1.6.4 Not encrypted packets
When a packet is not encrypted no MAC can be generated by EAX. Therefore
the SharedMac (see 3.2) will be used.

## 1.7 Packet Stack Wrap-up
This stack is a reference for the execution order of the set data operations.
For incoming packets the stack is executed bot to top, for outgoing packets
top to bot.

    +-----------+
    |   Data    |
    +-----------+
    | Compress  |
    +-----------+
    |   Split   |
    +-----------+
    |  Encrypt  |
    +-----------+

## 1.8 Packet Types Data Structures
The following chapter describes the data structure for different packet types.

### 1.8.1.1 Voice (Client -> Server)
    +--+--+--+---------//---------+
    | VId |C |        Data        |
    +--+--+--+---------//---------+

| Name | Type | Explanation     |
|------|------|-----------------|
| VId  | u16  | Voice Packet Id |
| C    | u8   | Codec Type      |
| Data | var  | Voice Data      |

### 1.8.1.2 Voice (Client <- Server)
    +--+--+--+--+--+---------//---------+
    | VId | CId |C |        Data        |
    +--+--+--+--+--+---------//---------+

| Name | Type | Explanation     |
|------|------|-----------------|
| VId  | u16  | Voice Packet Id |
| CId  | u16  | Talking Client  |
| C    | u8   | Codec Type      |
| Data | var  | Voice Data      |

### 1.8.2.1 VoiceWhisper
    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+---------//---------+
    | VId |C |N |M |           U*          |  T* |        Data        |
    +--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+---------//---------+

| Name | Type  | Explanation                           |
|------|-------|---------------------------------------|
| VId  | u16   | Voice Packet Id                       |
| C    | u8    | Codec Type                            |
| N    | u8    | Count of ChannelIds to send to        |
| M    | u8    | Count of ClientIds to send to         |
| U    | [u64] | Targeted ChannelIds, repeated N times |
| T    | [u16] | Targeted ClientIds, repeated M times  |
| Data | var   | Voice Data                            |

### 1.8.2.2 VoiceWhisper (Client <- Server)
    +--+--+--+--+--+---------//---------+
    | VId | CId |C |        Data        |
    +--+--+--+--+--+---------//---------+

| Name | Type | Explanation     |
|------|------|-----------------|
| VId  | u16  | Voice Packet Id |
| CId  | u16  | Talking Client  |
| C    | u8   | Codec Type      |
| Data | var  | Voice Data      |

### 1.8.3-4 Command and CommandLow
The TeamSpeak3 Query like command string encoded in UTF-8

### 1.8.5 Ping
Empty.

### 1.8.6-8 Pong, Ack and AckLow
    +--+--+
    | PId |
    +--+--+

| Name | Type | Explanation                        |
|------|------|------------------------------------|
| PId  | u16  | The packet id that is acknowledged |

- In case of `Pong` a matching ping packet id is acknowledged.
- In case of `Ack` or `AckLow` a matching Command or CommandLow packet id
respectively is acknowledged.

### 1.8.9 Init1
(see 2.1)-(see 2.5)

## 1.9 Packet Ids and Generations
### 1.9.1 Packet Ids
Each packet type and packet direction must be maintained by an own packet id
counter.
This means the client has 9 different packet id counter for outgoing packets.

For each new packet the counter gets increased by 1. This also applies to
splitted packets.

The client must also maintain packet ids for incoming packets in case of
packets arriving out of order.

All Packet Ids start at 1 unless otherwise specified.

### 1.9.2 Generations
Packet Ids are stored as u16, this mean that they range from 0-65536.

When the packet id overflows from 65535 to 0 at a packet,
the generation counter for this packet type gets increased by 1.

Note that the new generation id immediately applies to the 'overflowing' packet.

The generation id counter is solely used for encryption (see 1.6).

## 1.10 Packet Acknowledgement / Packet Loss
In order to reliably send packets over UPD some packet types must get
acknowledged when received (see 1.11).

The protocol uses selective repeat for lost packets. This means each packet has
its own timeout. Already acknowledged later packets must not be resent.
When a packet times out, the exact same packet should be resent until properly
acknowledged by the server.
If after 30 seconds no resent packet gets acknowledged the connection should be
closed.
Packet resend timeouts should be calculated with an exponential backoff to
prevent network congestion.

## 1.11 Wrap-up
| Type         | Acknowledged (by) | Resend | Encrypted | Splittable | Compressable |
|--------------|-------------------|--------|-----------|------------|--------------|
| Voice        | ✗                 | ✗      | Optional  | ✗          | ✗            |
| VoiceWhisper | ✗                 | ✗      | Optional  | ✗          | ✗            |
| Command      | ✓ (Ack)           | ✓      | ✓         | ✓          | ✓            |
| CommandLow   | ✓ (AckLow)        | ✓      | ✓         | ✓          | ✓            |
| Ping         | ✓ (Pong)          | ✗      | ✗         | ✗          | ✗            |
| Pong         | ✗                 | ✗      | ✗         | ✗          | ✗            |
| Ack          | ✗                 | ✓      | ✓         | ✗          | ✗            |
| AckLow       | ✗                 | ✓      | ✓         | ✗          | ✗            |
| Init1        | ✓ (next Init1)    | ✓      | ✗         | ✗          | ✗            |

# 2. The (Low-Level) Initiation/Handshake
A connection is started from the client by sending the first handshake
packet. The handshake process consists of 5 different init packets. This
includes the so called RSA puzzle to prevent DOS attacks.

The packet header values are set as following for all packets here:

| Parameter | Value                                                  |
|-----------|--------------------------------------------------------|
| MAC       | [u8]{ 0x54, 0x53, 0x33, 0x49, 0x4E, 0x49, 0x54, 0x31 } |
| key       | N/A                                                    |
| nonce     | N/A                                                    |
| Type      | Init1                                                  |
| Encrypted | ✗                                                      |
| Packet Id | u16: 101                                               |
| Client Id | u16: 0                                                 |

## 2.1 Packet 0 (Client -> Server)
    04 bytes : Version of the TeamSpeak client as timestamp
               Example: { 0x06, 0x3b, 0xec, 0xe9 }
    01 bytes : Init-packet step number
               Const: 0x00
    04 bytes : Current timestamp in unix format
    04 bytes : Random bytes := [A0]
    08 bytes : Zeros, reserved.

## 2.2 Packet 1 (Client <- Server)
    01 bytes : Init-packet step number
               Const: 0x01
    16 bytes : Server stuff := [A1]
    04 bytes : The bytes from [A0] in reversed order

## 2.3 Packet 2 (Client -> Server)
    04 bytes : Version of the TeamSpeak client as timestamp
    01 bytes : Init-packet step number
               Const: 0x02
    16 bytes : The bytes from [A1]
    04 bytes : The bytes from [A0] in reversed order

## 2.4 Packet 3 (Client <- Server)
     01 bytes : Init-packet step number
                Const: 0x03
     64 bytes : 'x', an unsigned BigInteger
     64 bytes : 'n', an unsigned BigInteger
     04 bytes : 'level' a u32
    100 bytes : Server stuff := [A2]

## 2.5 Packet 4 (Client -> Server)
     04 bytes : Version of the TeamSpeak client as timestamp
     01 bytes : Init-packet step number
                Const: 0x04
     64 bytes : the received 'x'
     64 bytes : the received 'n'
     04 bytes : the received 'level'
    100 bytes : The bytes from [A2]
     64 bytes : 'y' which is the result of x ^ (2 ^ level) % n as an unsigned
                BigInteger. Padded from the lower side with '0x00' when shorter
                than 64 bytes.
                Example: { 0x00, 0x00, data ... data}
    var bytes : The clientinitiv command data as explained in (see 3.1)


# 3. The (High-Level) Initiation/Handshake
In this phase the client and server exchange basic information and
agree on/calculate the symmetric AES encryption key with the ECDH
public/private key exchange technique.

Both the client and the server will need a EC public/private key. This key
is also the identity which the server uses to recognize a user again.
The curve used is 'prime256v1'.

All high level packets specified in this chapter are sent as `Command` Type
packets as explained in (see 2.8.3). Additionally the `Newprotocol` flag
(see 2.3) must be set on all `Command`, `CommandLow` and `Init1` packets.

The packet header values for (see 3.1) and (see 3.2) are as following:

| Parameter | Value                                                                                                |
|-----------|------------------------------------------------------------------------------------------------------|
| MAC       | (Generated by EAX)                                                                                   |
| key       | [u8]{0x63, 0x3A, 0x5C, 0x77, 0x69, 0x6E, 0x64, 0x6F, 0x77, 0x73, 0x5C, 0x73, 0x79, 0x73, 0x74, 0x65} |
| nonce     | [u8]{0x6D, 0x5C, 0x66, 0x69, 0x72, 0x65, 0x77, 0x61, 0x6C, 0x6C, 0x33, 0x32, 0x2E, 0x63, 0x70, 0x6C} |
| Type      | Command                                                                                              |
| Encrypted | ✓                                                                                                    |
| Packet Id | u16: 0                                                                                               |
| Client Id | u16: 0                                                                                               |

The acknowledgement packets use the same parameters as the commands, except with
the Type `Ack`.

_(Maybe add a #3.0 Prelude for required cryptographic values, if yes move the
omega ASN.1 encoding here)_

## 3.1 clientinitiv (Client -> Server)
The first packet is sent (Client -> Server) although this is only sent for
legacy reasons since newer servers (at least 3.0.13.0?) use the data part
embedded in the last `Init1` packet from the low-level handshake (see 1.5).

The ip parameter is added but left without value for legacy reasons.

    clientinitiv alpha={alpha} omega={omega} ip

- `alpha` is set to `base64(random[u8; 10])`  
  which are 10 random bytes for entropy.
- `omega` is set to `base64(publicKey[u8])`  
  omega is an ASN.1-DER encoded public key from the ECDH parameters as following:

  | Type       | Value          | Explanation                               |
  |------------|----------------|-------------------------------------------|
  | BIT STRING | 1bit, Value: 0 | LibTomCrypt uses 0 for a public key       |
  | INTEGER    | 32             | The LibTomCrypt used keysize              |
  | INTEGER    | publicKey.x    | The affine X-Coordinate of the public key |
  | INTEGER    | publicKey.y    | The affine Y-Coordinate of the public key |

## 3.2 initivexpand (Client <- Server)
The server responds with this command.

    initivexpand alpha={alpha} beta={beta} omega={omega}

- `alpha` must have the same value as sent to the server in the previous step.
- `beta` is set to `base64(random[u8; 10])`  
  by the server.
- `omega` is set to `base64(publicKey[u8])`  
  with the public Key from the server, encoded same as in (see 3.1)

With this information the client now must calculate the shared secret.

    let sharedSecret: ECPoint
    let x: [u8]
    let sharedData: [u8; 32]
    let SharedIV: [u8; 20]
    let SharedMac: [u8; 8]
    let ECDH(A, B)    := (A * B).Normalize

    sharedSecret         = ECDH(serverPublicKey, ownPrivateKey)
    x                    = sharedSecret.x.AsByteArray()
    if x.length < 32
        sharedData[0-(32-x.length)]  = [0..0]
        sharedData[(32-x.length)-32] = x[0-x.length]
    if x.length == 32
        sharedData[0-32] = x[0-32]
    if x.length > 32
        sharedData[0-32] = x[(x.length-32)-x.length]
    SharedIV             = SHA1(sharedData)
    SharedIV[0-10]       = SharedIV[0-10] xor alpha.decode64()
    SharedIV[10-20]      = SharedIV[10-20] xor beta.decode64()
    SharedMac[0-8]       = SHA1(SharedIV)[0-8]

**Notes**:
- Only `SharedIV` and `SharedMac` are needed. The other values can be discarded.
- The crypto handshake is now completed. The normal encryption scheme (see 1.6) is
  from now on used.
- All `Command`, `CommandLow`, `Ack` and `AckLow` packets must get encrypted.
- `Voice` packets (and `VoiceWhisper` when wanted) should be encrypted when the
channel encryption or server wide encryption flag is set.
- `Ping` and `Pong` must not be encrypted.

## 3.3 clientinit (Client -> Server)
    clientinit client_nickname client_version client_platform client_input_hardware client_output_hardware client_default_channel client_default_channel_password client_server_password client_meta_data client_version_sign client_key_offset client_nickname_phonetic client_default_token hwid

- `client_nickname` the desired nickname
- `client_version` the client version
- `client_platform` the client platform
- `client_input_hardware` whether a input device is available
- `client_output_hardware` whether a output device is available
- `client_default_channel` the default channel to join. This can be a channel
  path or `/<id>` (eg `/1`) for a channel id.
- `client_default_channel_password` the password for the join channel, prepared
  the following way `base64(sha1(password))`
- `client_server_password` the password to enter the server, prepared the
  following way `base64(sha1(password))`
- `client_meta_data` (can be left empty)
- `client_version_sign` a cryptographic sign to verify the genuinity of the
  client
- `client_key_offset` the number offset used to calculate the hashcash (see 4.1)
  value of the used identity 
- `client_nickname_phonetic` the phonetic nickname for text-to-speech
- `client_default_token` permission token to be used when connecting to a server
- `hwid` hardware identification string

**Notes**:
- Since client signs are only generated and distributed by TeamSpeak systems,
  this the recommended client triple, as it is the reference for this paper
  - Version: `3.0.19.3 [Build: 1466672534]`
  - Platform: `Windows`
  - Sign: `a1OYzvM18mrmfUQBUgxYBxYz2DUU6y5k3/mEL6FurzU0y97Bd1FL7+PRpcHyPkg4R+kKAFZ1nhyzbgkGphDWDg==`
- The `hwid` is usually around 30 characters long, but strings as short as only 
  few characters like `123,456` are accepted
- Parameters which are empty or not used must be declared but left without
  value and the `=` character

## 3.4 initserver (Client <- Server)
    initserver virtualserver_welcomemessage virtualserver_platform virtualserver_version virtualserver_maxclients virtualserver_created virtualserver_hostmessage virtualserver_hostmessage_mode virtualserver_id virtualserver_ip virtualserver_ask_for_privilegekey acn aclid pv lt client_talk_power client_needed_serverquery_view_power virtualserver_name virtualserver_codec_encryption_mode virtualserver_default_server_group virtualserver_default_channel_group virtualserver_hostbanner_url virtualserver_hostbanner_gfx_url virtualserver_hostbanner_gfx_interval virtualserver_priority_speaker_dimm_modificator virtualserver_hostbutton_tooltip virtualserver_hostbutton_url virtualserver_hostbutton_gfx_url virtualserver_name_phonetic virtualserver_icon_id virtualserver_hostbanner_mode virtualserver_channel_temp_delete_delay_default

- `virtualserver_welcomemessage` the welcome message of the sever
- `virtualserver_platform` the plattform the server is running on
- `virtualserver_version` the verison of the server
- `virtualserver_maxclients` the maximum allowed clients on this server
- `virtualserver_created` the start date of the server
- `virtualserver_hostmessage`
- `virtualserver_hostmessage_mode`
- `virtualserver_id`
- `virtualserver_ip`
- `virtualserver_ask_for_privilegekey`
- `acn` the accepted client nickname, this might differ from the desired
  nickname if it's already in use
- `aclid` the assigned client Id
- `pv` ???
- `lt` License Type of the server
- `client_talk_power` the initial talk power
- `client_needed_serverquery_view_power`
- `virtualserver_name`
- `virtualserver_codec_encryption_mode` see CodecEncryptionMode from the
  official query documentation
- `virtualserver_default_server_group`
- `virtualserver_default_channel_group`
- `virtualserver_hostbanner_url`
- `virtualserver_hostbanner_gfx_url`
- `virtualserver_hostbanner_gfx_interval`
- `virtualserver_priority_speaker_dimm_modificator`
- `virtualserver_hostbutton_tooltip`
- `virtualserver_hostbutton_url`
- `virtualserver_hostbutton_gfx_url`
- `virtualserver_name_phonetic`
- `virtualserver_icon_id`
- `virtualserver_hostbanner_mode`
- `virtualserver_channel_temp_delete_delay_default`

Note:
- From this point on the client knows his client id, therefore it must be set
  in the header of each packet

## 3.5 Further notifications
The server will now send all needed information to display the entire
server properly. Those notifications are in no fixed order, although they
 are most of the time sent in the here declared order.

### 3.5.1 channellist and channellistfinished
See the official query documentation to get further details to this
notifications parameter.

    channellist cid cpid channel_name channel_topic channel_codec channel_codec_quality channel_maxclients channel_maxfamilyclients channel_order channel_flag_permanent channel_flag_semi_permanent channel_flag_default channel_flag_password channel_codec_latency_factor channel_codec_is_unencrypted channel_delete_delay channel_flag_maxclients_unlimited channel_flag_maxfamilyclients_unlimited channel_flag_maxfamilyclients_inherited channel_needed_talk_power channel_forced_silence channel_name_phonetic channel_icon_id channel_flag_private

- `cid` Channel id
- `cpid` Channel parent id
- `channel_name`
- `channel_topic`
- `channel_codec` see the Codec enum from the official query documentation
- `channel_codec_quality` value between 0-10 representing a bitrate (see XXX)
- `channel_maxclients`
- `channel_maxfamilyclients`
- `channel_order`
- `channel_flag_permanent`
- `channel_flag_semi_permanent`
- `channel_flag_default`
- `channel_flag_password`
- `channel_codec_latency_factor`
- `channel_codec_is_unencrypted`
- `channel_delete_delay`
- `channel_flag_maxclients_unlimited`
- `channel_flag_maxfamilyclients_unlimited`
- `channel_flag_maxfamilyclients_inherited`
- `channel_needed_talk_power`
- `channel_forced_silence`
- `channel_name_phonetic`
- `channel_icon_id`
- `channel_flag_private`

After the last `channellist` notification the server will send

    channellistfinished

### 3.5.2 notifycliententerview
Same as the query notification.

### 3.5.3 notifychannelgrouplist
### 3.5.4 notifyservergrouplist
### 3.5.5 notifyclientneededpermissions

# 4. Further concepts
## 4.1 Hashcash
To prevent client spamming (connecting to a server with many different clients)
the server requires a certain hashcash level on each identity. This level has a
exponentially growing calculation time with increasing level. This ensures that
a user wanting to spam a certain server needs to invest some time into
calculating the required level.

- The publicKey is a string encoded as in (see 3.1) the omega value.
- The key offset is a u64 number, which gets converted to a string when
concatenated.

The first step is to calculate a hash as following

    let data: [u8; 20] = SHA1(publicKey + keyOffset)

The level can now be calculated by counting the continuous leading zero bits in
the data array.
The bytes in the array get counted from 0 to 20 and the bits in each byte
from least significant to most significant.

## 4.2 Uid
To calculate the uid of an identity the public key is required. Therefore you
can only calculate the uid of your own identity and the servers identity you
are connecting to.

The publicKey is a string encoded as in (see 3.1) the omega value.

The uid can be calculated as following

    let uid: string = base64(sha1(publicKey))

## 4.3 Ping/Pong
The server will regularly send ping packets to check if a client is still alive.
The client must answer them with the according pong packet.

The client should also send ping packets to the server to check for connection.
They will be answered with according pong packets.

Sending ping packets from the client side should not be started before the
crypto handshake has been completed (see 3.3)

## 4.? Differences between Query and Full Client
- notifyconnectioninforequest
- => setconnectioninfo
