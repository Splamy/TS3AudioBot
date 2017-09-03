TS3 PROTOCOL PAPER
==================

# 0. Naming Conventions
- `(Client -> Server)` denotes packets from client to server.
- `(Client <- Server)` denotes packets from server to client.

todo u16, datatypes etc

# 1. The (Low-Level) Initiation/Handshake

A connection is started from the client by sending the first handshake
packet. The handshake process consists of 5 different init packets. This
includes the so called RSA puzzle to prevent DOS attacks.

## Packet 0 (Client -> Server)
    04 bytes : Version of the Teamspeak client as timestamp
               Example: { 0x06, 0x3b, 0xec, 0xe9 }
    01 bytes : Init-packet step number
               Const: 0x00
    08 bytes : Zeros, reserverd.
    04 bytes : Current timestamp in unix format
    04 bytes : Random bytes := [A0]

## Packet 1 (Client <- Server)
    01 bytes : Init-packet step number
               Const: 0x01
    16 bytes : Server stuff := [A1]
    04 bytes : The bytes from [A0] in reversed order

## Packet 2 (Client -> Server)
    04 bytes : Version of the Teamspeak client as timestamp
    01 bytes : Init-packet step number
               Const: 0x02
    16 bytes : The bytes from [A1]
    04 bytes : The bytes from [A0] in reversed order

## Packet 3 (Client <- Server)
     01 bytes : Init-packet step number
                Const: 0x03
     64 bytes : 'x', an unsigned biginteger
     64 bytes : 'n', an unsigned biginteger
     04 bytes : 'level' an int4 parsed with network to host endianness
    100 bytes : Server stuff := [A2]

## Packet 4 (Client -> Server)
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
    var bytes : The clientinitiv command as explained in (* TODO *)

# 2. Low-Level Packets

## 2.1 Packet structure
- All packets are build in a fixed scheme,
though differently depending in which direction.
- Every column here represents 1 byte.
- All datatypes are sent in network order (Big Endian).
- The entire packet size must be at max 500 bytes.

### 2.1.1 (Client -> Server)
    +--+--+--+--+--+--+--+--+--+--+--+--+--+---------//----------+
    |          MAC          | PId | CId |PT|        Data         |
    +--+--+--+--+--+--+--+--+--+--+--+--+--+---------//----------+

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


# 3. The (High-Level) Initiation/Handshake

In this phase the client and server exchange basic information and
agree on/calculate the symmetric AES encryption key with the ECDH
public/private key exchange technique.

Both the client and the server will need a EC public/private key. This key
is also the identity which the server uses to recognize a user again.
The curve used is 'prime256v1'.


# 4. High-Level Commands