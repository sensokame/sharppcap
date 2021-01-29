/*
This file is part of SharpPcap.

SharpPcap is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

SharpPcap is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License
along with SharpPcap.  If not, see <http://www.gnu.org/licenses/>.
*/
/*
 * Copyright 2011 Chris Morgan <chmorgan@gmail.com>
 */

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SharpPcap.LibPcap
{
    /// <summary>
    /// Create or write to a pcap capture file
    ///
    /// NOTE: Appending to a capture file is not currently supported
    /// </summary>
    public class CaptureFileWriterDevice : PcapDevice
    {
        private readonly string m_pcapFile;

        /// <summary>
        /// Handle to an open dump file, not equal to IntPtr.Zero if a dump file is open
        /// </summary>
        protected IntPtr m_pcapDumpHandle = IntPtr.Zero;

        /// <summary>
        /// Whether dump file is open or not
        /// </summary>
        /// <returns>
        /// A <see cref="bool"/>
        /// </returns>
        protected bool DumpOpened
        {
            get
            {
                return (m_pcapDumpHandle != IntPtr.Zero);
            }
        }

        /// <value>
        /// The name of the capture file
        /// </value>
        public override string Name
        {
            get
            {
                return m_pcapFile;
            }
        }

        /// <value>
        /// Description of the device
        /// </value>
        public override string Description
        {
            get
            {
                return "Capture file reader device";
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public CaptureFileWriterDevice(string captureFilename, System.IO.FileMode mode = FileMode.OpenOrCreate)
        {
            m_pcapFile = captureFilename;

            // append isn't possible without some difficulty and not implemented yet
            if (mode == FileMode.Append)
            {
                throw new InvalidOperationException("FileMode.Append is not supported, please contact the developers if you are interested in helping to implementing it");
            }
        }

        /// <summary>
        /// Close the capture file
        /// </summary>
        public override void Close()
        {
            if (!Opened)
                return;

            base.Close();

            // close the dump handle
            if (m_pcapDumpHandle != IntPtr.Zero)
            {
                LibPcapSafeNativeMethods.pcap_dump_close(m_pcapDumpHandle);
                m_pcapDumpHandle = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Open the device
        /// </summary>
        public override void Open(DeviceConfiguration configuration)
        {
            if (configuration.Snaplen > Pcap.MAX_PACKET_SIZE)
            {
                var ex = new InvalidOperationException("Snaplen > Pcap.MAX_PACKET_SIZE");
                configuration.RaiseConfigurationFailed("snaplen", ex);

                throw ex;
            }

            // set the device handle
            PcapHandle = LibPcapSafeNativeMethods.pcap_open_dead((int)configuration.LinkLayerType, configuration.Snaplen);

            m_pcapDumpHandle = LibPcapSafeNativeMethods.pcap_dump_open(PcapHandle, m_pcapFile);
            if (m_pcapDumpHandle == IntPtr.Zero)
                throw new PcapException("Error opening dump file '" + LastError + "'");

            Active = true;
        }

        /// <summary>
        /// Retrieves pcap statistics
        ///
        /// Not currently supported for this device
        /// </summary>
        /// <returns>
        /// A <see cref="PcapStatistics"/>
        /// </returns>
        public override ICaptureStatistics Statistics => null;

        /// <summary>
        /// Writes a packet to the pcap dump file associated with this device.
        /// </summary>
        /// <param name="p">P.</param>
        /// <param name="h">The height.</param>
        public void Write(byte[] p, ref PcapHeader h)
        {
            ThrowIfNotOpen("Cannot dump packet, device is not opened");
            if (!DumpOpened)
                throw new DeviceNotReadyException("Cannot dump packet, dump file is not opened");

            //Marshal packet
            IntPtr pktPtr;
            pktPtr = Marshal.AllocHGlobal(p.Length);
            Marshal.Copy(p, 0, pktPtr, p.Length);

            //Marshal header
            IntPtr hdrPtr = h.MarshalToIntPtr();

            LibPcapSafeNativeMethods.pcap_dump(m_pcapDumpHandle, hdrPtr, pktPtr);

            Marshal.FreeHGlobal(pktPtr);
            Marshal.FreeHGlobal(hdrPtr);
        }

        /// <summary>
        /// Writes a packet to the pcap dump file associated with this device.
        /// </summary>
        /// <param name="p">The packet to write</param>
        public void Write(byte[] p)
        {
            var header = new PcapHeader(0, 0, (uint)p.Length, (uint)p.Length);
            Write(p, ref header);
        }

        /// <summary>
        /// Writes a packet to the pcap dump file associated with this device.
        /// </summary>
        /// <param name="p">The packet to write</param>
        public void Write(RawCapture p)
        {
            var data = p.Data;
            var timeval = p.Timeval;
            var header = new PcapHeader((uint)timeval.Seconds, (uint)timeval.MicroSeconds,
                                        (uint)data.Length, (uint)data.Length);
            Write(data, ref header);
        }

        public override void SendPacket(ReadOnlySpan<byte> p)
        {
            throw new NotSupportedOnCaptureFileException("Sending not supported on a capture file");
        }
    }
}
