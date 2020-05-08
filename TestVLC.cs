using System;
using System.Collections.Generic;
using System.Text;
using LibVLCSharp.Shared;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TestLibVLC
{

    class TestVLC
    {
        private MediaPlayer.LibVLCVideoFormatCb _videoFormat;
        private MediaPlayer.LibVLCVideoLockCb _lockCB;
        private MediaPlayer.LibVLCVideoUnlockCb _unlockCB;
        private MediaPlayer.LibVLCVideoDisplayCb _displayCB;
        private MediaPlayer.LibVLCVideoCleanupCb _cleanupVideoCB;

        private MediaPlayer _mediaPlayer = null;
        private GCHandle? _imageData = null;

        private Size _size;

        public async Task Run()
        {
            Core.Initialize(@"C:\Program Files\VideoLAN\VLC");

            _videoFormat = VideoFormat;
            _lockCB = LockVideo;
            _unlockCB = UnlockVideo;
            _displayCB = DisplayVideo;
            _cleanupVideoCB = CleanupVideo;

            var libVLC = new LibVLC();
            libVLC.Log += _libVLC_Log;
            var media = new Media(libVLC, "screen://", FromType.FromLocation);

            
            _mediaPlayer = new MediaPlayer(media);

            _mediaPlayer.SetVideoFormatCallbacks(_videoFormat, _cleanupVideoCB);
            _mediaPlayer.SetVideoCallbacks(_lockCB, _unlockCB, _displayCB);

            _mediaPlayer.EncounteredError += (sender, e) =>
            {
                Cleanup();
            };

            _mediaPlayer.EndReached += (sender, e) =>
            {
                Cleanup();
            };

            _mediaPlayer.Stopped += (sender, e) =>
            {
                Cleanup();
            };
            
            _mediaPlayer.Play();
            await Task.Delay(3000); //

            _mediaPlayer.Stop(); // crashes here

        }

        private static void _libVLC_Log(object sender, LogEventArgs e)
        {
            Debug.WriteLine("vlc: " + e.Message);
        }

        private void Cleanup()
        {

        }

        private IntPtr LockVideo(IntPtr userdata, IntPtr planes)
        {
            Marshal.WriteIntPtr(planes, userdata);
            return userdata;
        }
        private void UnlockVideo(IntPtr opaque, IntPtr picture, IntPtr planes)
        {
        }

        private void CleanupVideo(ref IntPtr opaque)
        {

        }

        private void DisplayVideo(IntPtr userdata, IntPtr picture)
        {
            
        }

        private uint GetAlignedDimension(uint dimension, uint mod)
        {
            var modResult = dimension % mod;
            if (modResult == 0)
            {
                return dimension;
            }

            return dimension + mod - (dimension % mod);
        }

        /// <summary>
        /// Called by vlc when the video format is needed. This method allocats the picture buffers for vlc and tells it to set the chroma to RV32
        /// </summary>
        /// <param name="userdata">The user data that will be given to the <see cref="LockVideo"/> callback. It contains the pointer to the buffer</param>
        /// <param name="chroma">The chroma</param>
        /// <param name="width">The visible width</param>
        /// <param name="height">The visible height</param>
        /// <param name="pitches">The buffer width</param>
        /// <param name="lines">The buffer height</param>
        /// <returns>The number of buffers allocated</returns>
        private uint VideoFormat(ref IntPtr userdata, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
        {
            Debug.WriteLine("VideoFormat");
            ToFourCC("RV32", chroma);

            //Correct video width and height according to TrackInfo
            _size = new Size((int)width, (int)height);
            var md = _mediaPlayer.Media;
            foreach (MediaTrack track in md.Tracks)
            {
                if (track.TrackType == TrackType.Video)
                {
                    var trackInfo = track.Data;
                    if (trackInfo.Video.Width > 0 && trackInfo.Video.Height > 0)
                    {
                        width = trackInfo.Video.Width;
                        height = trackInfo.Video.Height;
                        _size = new Size((int)width, (int)height);
                        if (trackInfo.Video.SarDen != 0)
                        {
                            width = width * trackInfo.Video.SarNum / trackInfo.Video.SarDen;
                        }
                    }

                    break;
                }
            }

            pitches = this.GetAlignedDimension((uint)(width * 32) / 8, 32);
            lines = this.GetAlignedDimension(height, 32);

            var b = new byte[width * height * 32];
            if (_imageData != null)
                _imageData?.Free();
            _imageData = GCHandle.Alloc(b, GCHandleType.Pinned);
            userdata = ((GCHandle)_imageData).AddrOfPinnedObject();

            return 1;
        }

        /// <summary>
        /// Converts a 4CC string representation to its UInt32 equivalent
        /// </summary>
        /// <param name="fourCCString">The 4CC string</param>
        /// <returns>The UInt32 representation of the 4cc</returns>
        static void ToFourCC(string fourCCString, IntPtr destination)
        {
            if (fourCCString.Length != 4)
            {
                throw new ArgumentException("4CC codes must be 4 characters long", nameof(fourCCString));
            }

            var bytes = Encoding.ASCII.GetBytes(fourCCString);

            for (var i = 0; i < 4; i++)
            {
                Marshal.WriteByte(destination, i, bytes[i]);
            }
        }
    }
}
