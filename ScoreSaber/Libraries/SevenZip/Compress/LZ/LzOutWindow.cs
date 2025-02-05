#region

using System.IO;

#endregion

namespace ScoreSaber.Libraries.SevenZip.Compress.LZ {
    public class OutWindow {
        private byte[] _buffer;
        private uint _pos;
        private Stream _stream;
        private uint _streamPos;
        private uint _windowSize;

        public void Create(uint windowSize) {
            if (_windowSize != windowSize) {
                // System.GC.Collect();
                _buffer = new byte[windowSize];
            }

            _windowSize = windowSize;
            _pos = 0;
            _streamPos = 0;
        }

        public void Init(Stream stream, bool solid) {
            ReleaseStream();
            _stream = stream;
            if (!solid) {
                _streamPos = 0;
                _pos = 0;
            }
        }

        public void Init(Stream stream) { Init(stream, false); }

        public void ReleaseStream() {
            Flush();
            _stream = null;
        }

        public void Flush() {
            uint size = _pos - _streamPos;
            if (size == 0) {
                return;
            }

            _stream.Write(_buffer, (int)_streamPos, (int)size);
            if (_pos >= _windowSize) {
                _pos = 0;
            }

            _streamPos = _pos;
        }

        public void CopyBlock(uint distance, uint len) {
            uint pos = _pos - distance - 1;
            if (pos >= _windowSize) {
                pos += _windowSize;
            }

            for (; len > 0; len--) {
                if (pos >= _windowSize) {
                    pos = 0;
                }

                _buffer[_pos++] = _buffer[pos++];
                if (_pos >= _windowSize) {
                    Flush();
                }
            }
        }

        public void PutByte(byte b) {
            _buffer[_pos++] = b;
            if (_pos >= _windowSize) {
                Flush();
            }
        }

        public byte GetByte(uint distance) {
            uint pos = _pos - distance - 1;
            if (pos >= _windowSize) {
                pos += _windowSize;
            }

            return _buffer[pos];
        }
    }
}