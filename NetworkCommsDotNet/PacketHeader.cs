﻿//  Copyright 2009-2014 Marc Fletcher, Matthew Dean
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
//  A commercial license of this software can also be purchased. 
//  Please see <http://www.networkcomms.net/licensing/> for details.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using NetworkCommsDotNet.DPSBase;

using Serializer = NetworkCommsDotNet.DPSBase.DataSerializer;

namespace NetworkCommsDotNet
{
    /// <summary>
    /// Any <see cref="PacketHeader"/> options which are stored as a long.
    /// </summary>
    public enum PacketHeaderLongItems
    {
        /// <summary>
        /// The total size of the packet data payload in bytes. This is a compulsory option.
        /// </summary>
        TotalPayloadSize,

        /// <summary>
        /// The data serialiser and data processor used to unwrap the payload. Used as flags.
        /// </summary>
        SerializerProcessors,

        /// <summary>
        /// The creation time of the packet header.
        /// </summary>
        PacketCreationTime,

        /// <summary>
        /// The sequence number for this packet. Each connection maintains a unique counter which is increments on each sent packet.
        /// </summary>
        PacketSequenceNumber,
    }

    /// <summary>
    /// Any <see cref="PacketHeader"/> options which are stored as a string.
    /// </summary>
    public enum PacketHeaderStringItems
    {
        /// <summary>
        /// The type of the packet. This is a compulsory option which determines how the incoming packet is handled.
        /// </summary>
        PacketType,

        /// <summary>
        /// Specifies if a receive confirmation is required for this packet. String option as takes up less space for a boolean option.
        /// </summary>
        ReceiveConfirmationRequired,

        /// <summary>
        /// The packet type which should be used for any return packet type.
        /// </summary>
        RequestedReturnPacketType,

        /// <summary>
        /// A checksum corresponding to the payload data.
        /// </summary>
        CheckSumHash,

        /// <summary>
        /// The network identifier of the packet source
        /// </summary>
        SourceNetworkIdentifier,

        /// <summary>
        /// Optional packet identifier.
        /// </summary>
        PacketIdentifier,
    }

    /// <summary>
    /// Contains information required to send, receive and correctly rebuild any objects sent via NetworkComms.Net.
    /// Any data sent via NetworkCommsDotNet is always preceded by a packetHeader.
    /// </summary>
    public sealed class PacketHeader : IExplicitlySerialize
    {
        Dictionary<PacketHeaderLongItems, long> longItems;
        Dictionary<PacketHeaderStringItems, string> stringItems;
        
        /// <summary>
        /// Blank constructor required for deserialisation
        /// </summary>
#if iOS || ANDROID
        public PacketHeader() { }
#else
        private PacketHeader() { }
#endif

        /// <summary>
        /// Creates a new packetHeader
        /// </summary>
        /// <param name="packetTypeStr">The packet type to be used.</param>
        /// <param name="payloadPacketSize">The size on bytes of the payload</param>
        /// <param name="sendReceiveOptions">Send receive options which may contain header relevant options.</param>
        /// <param name="requestedReturnPacketTypeStr">An optional field representing the expected return packet type</param>
        /// <param name="checkSumHash">An optional field representing the payload checksum</param>
        public PacketHeader(string packetTypeStr, long payloadPacketSize, SendReceiveOptions sendReceiveOptions = null, string requestedReturnPacketTypeStr = null, string checkSumHash = null)
        {
            longItems = new Dictionary<PacketHeaderLongItems, long>();
            stringItems = new Dictionary<PacketHeaderStringItems, string>();

            stringItems.Add(PacketHeaderStringItems.PacketType, packetTypeStr);
            longItems.Add(PacketHeaderLongItems.TotalPayloadSize, payloadPacketSize);

            if (payloadPacketSize < 0)
                throw new Exception("payloadPacketSize can not be less than 0.");

            if (requestedReturnPacketTypeStr != null)
                stringItems.Add(PacketHeaderStringItems.RequestedReturnPacketType, requestedReturnPacketTypeStr);

            if (checkSumHash != null)
                stringItems.Add(PacketHeaderStringItems.CheckSumHash, checkSumHash);

            if (sendReceiveOptions != null)
            {
                if (sendReceiveOptions.Options.ContainsKey("ReceiveConfirmationRequired"))
                    stringItems.Add(PacketHeaderStringItems.ReceiveConfirmationRequired, "");

                if (sendReceiveOptions.Options.ContainsKey("IncludePacketConstructionTime"))
                    longItems.Add(PacketHeaderLongItems.PacketCreationTime, DateTime.Now.Ticks);
            }
        }

        internal PacketHeader(MemoryStream packetData, SendReceiveOptions sendReceiveOptions)
        {
            try
            {
                if (packetData == null) throw new ArgumentNullException("packetData", "Provided MemoryStream parameter cannot be null.");
                if (sendReceiveOptions == null) throw new ArgumentNullException("sendReceiveOptions", "Provided SendReceiveOptions parameter cannot be null.");

                if (packetData.Length == 0)
                    throw new SerialisationException("Attempted to create packetHeader using 0 packetData bytes.");

                PacketHeader tempObject = sendReceiveOptions.DataSerializer.DeserialiseDataObject<PacketHeader>(packetData, sendReceiveOptions.DataProcessors, sendReceiveOptions.Options);
                if (tempObject == null || !tempObject.longItems.ContainsKey(PacketHeaderLongItems.TotalPayloadSize) || !tempObject.stringItems.ContainsKey(PacketHeaderStringItems.PacketType))
                    throw new SerialisationException("Something went wrong when trying to deserialise the packet header object");
                else
                {
                    stringItems = new Dictionary<PacketHeaderStringItems, string>();
                    foreach (var pair in tempObject.stringItems)
                        stringItems.Add(pair.Key, pair.Value);
                    
                    longItems = new Dictionary<PacketHeaderLongItems, long>();
                    foreach (var pair in tempObject.longItems)
                        longItems.Add(pair.Key, pair.Value);
                }
            }
            catch (Exception ex)
            {
                throw new SerialisationException("Error deserialising packetHeader. " + ex.ToString());
            }
        }

        #region Get & Set
        /// <summary>
        /// The total size in bytes of the payload.
        /// </summary>
        public int TotalPayloadSize
        {
            get { return (int)longItems[PacketHeaderLongItems.TotalPayloadSize]; }
        }

        /// <summary>
        /// The packet type.
        /// </summary>
        public string PacketType
        {
            get { return stringItems[PacketHeaderStringItems.PacketType]; }
        }

        /// <summary>
        /// Check if a string option has been set.
        /// </summary>
        /// <param name="option">The string option to be checked.</param>
        /// <returns>Returns true if the provided string option has been set.</returns>
        public bool ContainsOption(PacketHeaderStringItems option)
        {
            return stringItems.ContainsKey(option);
        }

        /// <summary>
        /// Check if a long option has been set.
        /// </summary>
        /// <param name="option">The long option to be checked.</param>
        /// <returns>Returns true if the provided long option has been set.</returns>
        public bool ContainsOption(PacketHeaderLongItems option)
        {
            return longItems.ContainsKey(option);
        }

        /// <summary>
        /// Get a long option.
        /// </summary>
        /// <param name="option">The option to get</param>
        /// <returns>The requested long option</returns>
        public long GetOption(PacketHeaderLongItems option)
        {
            return longItems[option];
        }

        /// <summary>
        /// Get a string option
        /// </summary>
        /// <param name="options">The option to get</param>
        /// <returns>The requested string option</returns>
        public string GetOption(PacketHeaderStringItems options)
        {
            return stringItems[options];
        }

        /// <summary>
        /// Set a long option with the provided value.
        /// </summary>
        /// <param name="option">The option to set</param>
        /// <param name="Value">The option value</param>
        public void SetOption(PacketHeaderLongItems option, long Value)
        {
            longItems[option] = Value;
        }

        /// <summary>
        /// Set a string option with the provided value.
        /// </summary>
        /// <param name="option">The option to set</param>
        /// <param name="Value">The option value</param>
        public void SetOption(PacketHeaderStringItems option, string Value)
        {
            stringItems[option] = Value;
        }
        #endregion

        #region IExplicitlySerialize Members

        public void Serialize(Stream outputStream)
        {
            List<byte[]> data = new List<byte[]>();

            byte[] longItemsLengthData = BitConverter.GetBytes(longItems.Count); data.Add(longItemsLengthData);

            foreach (var pair in longItems)
            {
                byte[] keyData = BitConverter.GetBytes((int)pair.Key); data.Add(keyData);
                byte[] valData = BitConverter.GetBytes(pair.Value); data.Add(valData);
            }

            byte[] stringItemsLengthData = BitConverter.GetBytes(stringItems.Count); data.Add(stringItemsLengthData);

            foreach (var pair in stringItems)
            {
                byte[] keyData = BitConverter.GetBytes((int)pair.Key); data.Add(keyData);
                byte[] valData = Encoding.UTF8.GetBytes(pair.Value);
                byte[] valLengthData = BitConverter.GetBytes(valData.Length);

                data.Add(valLengthData);
                data.Add(valData);
            }

            foreach (byte[] datum in data)
                outputStream.Write(datum, 0, datum.Length);
        }

        public void Deserialize(Stream inputStream)
        {
            longItems = new Dictionary<PacketHeaderLongItems, long>();
            stringItems = new Dictionary<PacketHeaderStringItems, string>();

            byte[] longItemsLengthData = new byte[sizeof(int)]; inputStream.Read(longItemsLengthData, 0, sizeof(int));
            int longItemsLength = BitConverter.ToInt32(longItemsLengthData, 0);

            for(int i = 0; i < longItemsLength; i++)
            {
                byte[] keyData = new byte[sizeof(int)]; inputStream.Read(keyData, 0, sizeof(int));
                PacketHeaderLongItems key = (PacketHeaderLongItems)BitConverter.ToInt32(keyData, 0);

                byte[] valData = new byte[sizeof(long)]; inputStream.Read(valData, 0, sizeof(long));
                long val = BitConverter.ToInt64(valData, 0);

                longItems.Add(key, val);
            }

            byte[] stringItemsLengthData = new byte[sizeof(int)]; inputStream.Read(stringItemsLengthData, 0, sizeof(int));
            int stringItemsLength = BitConverter.ToInt32(stringItemsLengthData, 0);

            for (int i = 0; i < stringItemsLength; i++)
            {
                byte[] keyData = new byte[sizeof(int)]; inputStream.Read(keyData, 0, sizeof(int));
                PacketHeaderStringItems key = (PacketHeaderStringItems)BitConverter.ToInt32(keyData, 0);

                byte[] valLengthData = new byte[sizeof(int)]; inputStream.Read(valLengthData, 0, sizeof(int));
                int valLength = BitConverter.ToInt32(valLengthData, 0);

                byte[] valData = new byte[valLength]; inputStream.Read(valData, 0, valData.Length);
                string val = new String(Encoding.UTF8.GetChars(valData));
            }
        }

        #endregion
    }
}
