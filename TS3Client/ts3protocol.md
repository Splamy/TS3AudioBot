TS3 PROTOCOL PAPER
==================

# 0. Naming Conventions
- `(Client -> Server)` denotes packets from client to server.
- `(Client <- Server)` denotes packets from server to client.
- All datatypes are sent in network order (Big Endian) unless othwise specified.
- Datatypes are declared with a prefixing `u` or `i` for unsigend and signed
and a number for the bitlength.  
For example `u8` would be be the C equivalent of `uint8` or `unsigned char`

# 1. The (Low-Level) Initiation/Handshake

A connection is started from the client by sending the first handshake
packet. The handshake process consists of 5 different init packets. This
includes the so called RSA puzzle to prevent DOS attacks.

## 1.1 Packet 0 (Client -> Server)
    04 bytes : Version of the Teamspeak client as timestamp
               Example: { 0x06, 0x3b, 0xec, 0xe9 }
    01 bytes : Init-packet step number
               Const: 0x00
    08 bytes : Zeros, reserverd.
    04 bytes : Current timestamp in unix format
    04 bytes : Random bytes := [A0]

## 1.2 Packet 1 (Client <- Server)
    01 bytes : Init-packet step number
               Const: 0x01
    16 bytes : Server stuff := [A1]
    04 bytes : The bytes from [A0] in reversed order

## 1.3 Packet 2 (Client -> Server)
    04 bytes : Version of the Teamspeak client as timestamp
    01 bytes : Init-packet step number
               Const: 0x02
    16 bytes : The bytes from [A1]
    04 bytes : The bytes from [A0] in reversed order

## 1.4 Packet 3 (Client <- Server)
     01 bytes : Init-packet step number
                Const: 0x03
     64 bytes : 'x', an unsigned biginteger
     64 bytes : 'n', an unsigned biginteger
     04 bytes : 'level' a u32
    100 bytes : Server stuff := [A2]

## 1.5 Packet 4 (Client -> Server)
     04 bytes : Version of the Teamspeak client as timestamp
     01 bytes : Init-packet step number
                Const: 0x04
     64 bytes : the received 'x'
     64 bytes : the received 'n'
     04 bytes : the received 'level'
    100 bytes : The bytes from [A2]
     64 bytes : 'y' which is the result of x ^ (2 ^ level) % n as an unsigned
                biginteger. Padded from the lower side with '0x00' when shorter
                than 64 bytes.
                Example: { 0x00, 0x00, data ... data}
    var bytes : The clientinitiv command as explained in (see XXX)

# 2. Low-Level Packets

## 2.1 Packet structure
- The packets are build in a fixed scheme,
though have differences depending in which direction.
- Every column here represents 1 byte.
- The entire packet size must be at max 500 bytes.

### 2.1.1 (Client -> Server)
    +--+--+--+--+--+--+--+--+--+--+--+--+--+---------//----------+
    |          MAC          | PId | CId |PT|        Data         |
    +--+--+--+--+--+--+--+--+--+--+--+--+--+---------//----------+
                            \    Header    /

| Name | Size        | Datatype | Explanation                     |
|------|-------------|----------|---------------------------------|
| MAC  | 8 bytes     | [u8]     | EAX Message Authentication Code |
| PId  | 2 bytes     | u16      | Packet Id                       |
| CId  | 2 bytes     | u16      | Client Id                       |
| PT   | 1 byte      | u8       | Packet Type + Flags             |
| Data | <=487 bytes | [u8]     | The packet payload              |

### 2.1.2 (Client <- Server)
    +--+--+--+--+--+--+--+--+--+--+--+------------//-------------+
    |          MAC          | PId |PT|           Data            |
    +--+--+--+--+--+--+--+--+--+--+--+------------//-------------+
                            \ Header /

| Name | Size        | Datatype | Explanation                     |
|------|-------------|----------|---------------------------------|
| MAC  | 8 bytes     | [u8]     | EAX Message Authentication Code |
| PId  | 2 bytes     | u16      | Packet Id                       |
| PT   | 1 byte      | u8       | Packet Type + Flags             |
| Data | <=489 bytes | [u8]     | The packet payload              |

## 2.2 Packet Types
- `0x00` Voice
- `0x01` VoiceWhisper
- `0x02` Command
- `0x03` CommandLow
- `0x04` Ping
- `0x05` Pong
- `0x06` Ack
- `0x07` AckLow
- `0x08` Init1

## 2.3 Packet Type + Flags byte
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

## 2.4 Packet Compressing
To reduce a packet size the data can be compressed.
When the data is compressed the `Compressed` flag must be set.
The algorithm "QuickLZ" is used for compression.
QuickLZ offers different compression levels.
The choosen level differs depending on the packet direction as following
- (Client -> Server) Level 1
- (Client <- Server) Level 3

## 2.5 Packet Splitting
When the packet payload exceeds the maximum datablock size the data can be
split up across multiple packets.
When splitting occours, the `Fragmented` flag must be set on the first and
the last packet. Other flags, if set, are only set on the first packet.
The data can additionally be compressed before splitting.

## 2.6 Packet Encrypting
When a packet is not encrypted the `Unencrypted` flag is set. For encrypted
packets the flag gets cleared.
Packtes get encrypted with EAX mode (AES-CTR with OMAC).
The en/decryption parameters get generated for each packet as follows

### 2.6.1 Inputs

| Name | Type     | Explanation                   |
|------|----------|-------------------------------|
| PT   | u8       | Packet Type                   |
| PId  | u16      | Packet Id                     |
| PGId | u32      | Packet GenerationId (see XXX) |
| PD   | bool     | Packet Direction              |
| SIV  | [u8; 20] | Shared IV (see XXX)           |

### 2.6.2 Generation pseudocode

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
    key[0-2]        = key[0-2] xor (PId in network order)[0-2]

### 2.6.3 Encryption

The data can now be encrypted with the `key` and `nonce` from 2.6.2 as the EAX
key and nonce and the packet `Header` as defined in 2.1 as the EAX header
(sometimes called "Associated Text"). The resulting EAX mac
(sometimes called "Tag") will be stored in the `MAC` field as defined in 2.1.

## 2.7 Packet Stack Wrapup

This stack is a reference for the execution order of the set data operations.
For incomming packets the stack is executed bot to top, for outgoing packets
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

## 2.8 Packet Types Data Structures

The following chapter descibes the data structure for different packet types.

### 2.8.1 Voice
    +--+--+--+---------//---------+
    | VId |C |        Data        |
    +--+--+--+---------//---------+

| Name | Type | Explanation     |
|------|------|-----------------|
| VId  | u16  | Voice Packet Id |
| C    | u8   | Codec Type      |
| Data | var  | Voice Data      |

### 2.8.2 VoiceWhisper
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

### 2.8.3-4 Command and CommandLow
The TeamSpeak3 Query like command string encoded in UTF-8

### 2.8.5 Ping
Empty.

### 2.8.6-8 Pong, Ack and AckLow
    +--+--+
    | PId |
    +--+--+

| Name | Type | Explanation                        |
|------|------|------------------------------------|
| PId  | u16  | The packet id that is acknowledged |

- In case of `Pong` a matching ping packet id is acknowledged.
- In case of `Ack` or `AckLow` a matching Command or CommandLow packet id
respectively is acknowledged.

### 2.8.9 Init1
See 1.1-1.5

## 2.9 Packet Loss

# 3. The (High-Level) Initiation/Handshake

In this phase the client and server exchange basic information and
agree on/calculate the symmetric AES encryption key with the ECDH
public/private key exchange technique.

Both the client and the server will need a EC public/private key. This key
is also the identity which the server uses to recognize a user again.
The curve used is 'prime256v1'.

## 3.1 clientinitiv
alpha, omega, ip

## 3.1 initivexpand
alpha, beta, omega

## 3.1 clientinit

## 3.1 initserver

## 3.1 Further notifications
 - channellist
 - notifycliententerview
 - channellistfinished
 - notifychannelgrouplist
 - notifyservergrouplist 
 - notifyclientneededpermissions 

# 4. High-Level Commands

4.? Selective Repeat pack loss

4.? Ping/Pong

4.? Voice

4.? Uid

4.? Full client only concepts
- notifyconnectioninforequest
- => setconnectioninfo

4.? Differences between Query and Full CLient