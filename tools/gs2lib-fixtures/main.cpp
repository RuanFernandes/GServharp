#include <algorithm>
#include <array>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <iomanip>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

#include "CFileQueue.h"
#include "CEncryption.h"
#include "CSocket.h"

#if defined(_WIN32) || defined(_WIN64)
#include <winsock2.h>
#include <ws2tcpip.h>
#else
#include <arpa/inet.h>
#include <netinet/in.h>
#include <sys/socket.h>
#include <unistd.h>
#endif

namespace
{
    struct SocketPair
    {
        CSocket sendSocket;
        SOCKET receiveHandle = INVALID_SOCKET;

        ~SocketPair()
        {
#if defined(_WIN32) || defined(_WIN64)
            if (receiveHandle != INVALID_SOCKET)
                closesocket(receiveHandle);
#else
            if (receiveHandle != INVALID_SOCKET)
                close(receiveHandle);
#endif
        }
    };

    void closeRawSocket(SOCKET handle)
    {
        if (handle == INVALID_SOCKET)
            return;
#if defined(_WIN32) || defined(_WIN64)
        closesocket(handle);
#else
        close(handle);
#endif
    }

    SocketPair makeSocketPair()
    {
        CSocket winsockInitializer;

        SOCKET listener = ::socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        if (listener == INVALID_SOCKET)
            throw std::runtime_error("socket(listener) failed");

        sockaddr_in addr{};
        addr.sin_family = AF_INET;
        addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
        addr.sin_port = 0;

        if (::bind(listener, reinterpret_cast<sockaddr*>(&addr), sizeof(addr)) != 0)
            throw std::runtime_error("bind(listener) failed");
        if (::listen(listener, 1) != 0)
            throw std::runtime_error("listen(listener) failed");

        sockaddr_in bound{};
        socklen_t boundLen = sizeof(bound);
        if (::getsockname(listener, reinterpret_cast<sockaddr*>(&bound), &boundLen) != 0)
            throw std::runtime_error("getsockname(listener) failed");

        SOCKET client = ::socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        if (client == INVALID_SOCKET)
            throw std::runtime_error("socket(client) failed");

        if (::connect(client, reinterpret_cast<sockaddr*>(&bound), sizeof(bound)) != 0)
            throw std::runtime_error("connect(client) failed");

        SOCKET accepted = ::accept(listener, nullptr, nullptr);
        closeRawSocket(listener);
        if (accepted == INVALID_SOCKET)
            throw std::runtime_error("accept(listener) failed");

        sock_properties props;
        props.handle = accepted;
        props.protocol = SOCKET_PROTOCOL_TCP;
        props.type = SOCKET_TYPE_CLIENT;
        props.state = SOCKET_STATE_CONNECTED;

        SocketPair pair;
        pair.receiveHandle = client;
        pair.sendSocket.setProperties(props);
        return pair;
    }

    std::vector<std::uint8_t> readAvailable(SOCKET handle)
    {
        std::vector<std::uint8_t> output;
        std::array<char, 65536> buffer{};
        int received = ::recv(handle, buffer.data(), static_cast<int>(buffer.size()), 0);
        if (received > 0)
        {
            output.insert(output.end(), buffer.begin(), buffer.begin() + received);
        }
        return output;
    }

    std::string toHex(const std::vector<std::uint8_t>& bytes)
    {
        std::ostringstream oss;
        for (std::size_t i = 0; i < bytes.size(); ++i)
        {
            if (i != 0)
                oss << ' ';
            oss << std::uppercase << std::hex << std::setw(2) << std::setfill('0') << static_cast<int>(bytes[i]);
        }
        return oss.str();
    }

    std::vector<std::uint8_t> toBytes(const std::string& text)
    {
        return std::vector<std::uint8_t>(text.begin(), text.end());
    }

    CString toCString(const std::vector<std::uint8_t>& bytes)
    {
        CString output;
        for (auto byte : bytes)
            output.writeChar(static_cast<char>(byte));
        return output;
    }

    std::vector<std::uint8_t> toVector(const CString& text)
    {
        std::vector<std::uint8_t> output;
        output.reserve(text.length());
        for (int i = 0; i < text.length(); ++i)
            output.push_back(static_cast<std::uint8_t>(text[i]));
        return output;
    }

    std::vector<std::uint8_t> runFixture(unsigned int gen, unsigned char key, const std::string& payload)
    {
        auto pair = makeSocketPair();

        CFileQueue queue(&pair.sendSocket);
        queue.setCodec(gen, key);
        queue.addPacket(CString(payload));
        queue.sendCompress(false);

        return readAvailable(pair.receiveHandle);
    }

    void emitFixture(const std::string& name, unsigned int gen, unsigned char key, const std::string& payload)
    {
        auto output = runFixture(gen, key, payload);
        auto input = toBytes(payload);
        std::cout
            << name
            << "|gen=" << gen
            << "|key=" << static_cast<int>(key)
            << "|input=" << toHex(input)
            << "|output=" << toHex(output)
            << "\n";
    }

    std::vector<std::uint8_t> decodeInbound(unsigned int gen, unsigned char key, std::vector<std::uint8_t> framePayload)
    {
        CString packet = toCString(framePayload);
        CEncryption codec;
        codec.setGen(gen);
        codec.reset(key);

        switch (gen)
        {
            case ENCRYPT_GEN_1:
            case ENCRYPT_GEN_6:
                break;

            case ENCRYPT_GEN_2:
            case ENCRYPT_GEN_3:
                packet.zuncompressI();
                break;

            case ENCRYPT_GEN_4:
                codec.limitFromType(COMPRESS_BZ2);
                codec.decrypt(packet);
                packet.bzuncompressI();
                break;

            default:
            {
                int compressionType = packet.readChar();
                packet.removeI(0, 1);
                codec.limitFromType(static_cast<std::uint8_t>(compressionType));
                codec.decrypt(packet);
                if (compressionType == COMPRESS_ZLIB)
                    packet.zuncompressI();
                else if (compressionType == COMPRESS_BZ2)
                    packet.bzuncompressI();
                break;
            }
        }

        return toVector(packet);
    }

    void emitInboundFixture(const std::string& name, unsigned int gen, unsigned char key, const std::string& payload)
    {
        auto socketOutput = runFixture(gen, key, payload);
        std::vector<std::uint8_t> framePayload(socketOutput.begin() + 2, socketOutput.end());
        auto decoded = decodeInbound(gen, key, framePayload);
        std::cout
            << "inbound-" << name
            << "|gen=" << gen
            << "|key=" << static_cast<int>(key)
            << "|framePayload=" << toHex(framePayload)
            << "|decoded=" << toHex(decoded)
            << "\n";
    }
}

int main()
{
    try
    {
        emitFixture("gen2-short-abc-newline", ENCRYPT_GEN_2, 0, "abc\n");
        emitFixture("gen2-long-100a-newline", ENCRYPT_GEN_2, 0, std::string(100, 'a') + "\n");
        emitFixture("gen3-short-abc-newline", ENCRYPT_GEN_3, 0, "abc\n");
        emitFixture("gen3-long-100a-newline", ENCRYPT_GEN_3, 0, std::string(100, 'a') + "\n");
        emitFixture("gen4-short-abc-newline", ENCRYPT_GEN_4, 0, "abc\n");
        emitFixture("gen5-short-abc-newline", ENCRYPT_GEN_5, 0, "abc\n");
        emitFixture("gen5-threshold-55a-newline", ENCRYPT_GEN_5, 0, std::string(54, 'a') + "\n");
        emitFixture("gen5-zlib-56a-newline", ENCRYPT_GEN_5, 0, std::string(55, 'a') + "\n");
        emitFixture("gen5-bz2-8193a-newline", ENCRYPT_GEN_5, 0, std::string(8192, 'a') + "\n");
        emitInboundFixture("gen2-short-abc-newline", ENCRYPT_GEN_2, 0, "abc\n");
        emitInboundFixture("gen4-short-abc-newline", ENCRYPT_GEN_4, 0, "abc\n");
        emitInboundFixture("gen5-short-abc-newline", ENCRYPT_GEN_5, 0, "abc\n");
        emitInboundFixture("gen5-zlib-56a-newline", ENCRYPT_GEN_5, 0, std::string(55, 'a') + "\n");
    }
    catch (const std::exception& ex)
    {
        std::cerr << "fixture harness failed: " << ex.what() << "\n";
        return 1;
    }

    return 0;
}
