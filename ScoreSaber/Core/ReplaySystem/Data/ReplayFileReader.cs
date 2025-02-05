﻿#region

using ScoreSaber.Libraries.SevenZip.Compress.LzmaAlone;
using System;
using System.Collections.Generic;
using System.Text;

#endregion

namespace ScoreSaber.Core.ReplaySystem.Data {
    internal class Pointers {
        internal int ComboKeyframes;
        internal int EnergyKeyframes;
        internal int FPSKeyframes;
        internal int HeightKeyframes;
        internal int Metadata;
        internal int MultiplierKeyframes;
        internal int NoteKeyframes;
        internal int PoseKeyframes;
        internal int ScoreKeyframes;
    }

    internal class ReplayFileReader {
        private byte[] _input;

        internal ReplayFile Read(byte[] input) {
            List<byte> temp = new List<byte>();
            temp.AddRange(input);
            temp.RemoveRange(0, 28);
            _input = temp.ToArray();
            _input = SevenZipHelper.Decompress(_input);
            Pointers pointers = ReadPointers();

            ReplayFile file = new ReplayFile {
                Metadata = ReadMetadata(ref pointers.Metadata),
                PoseKeyframes = ReadPoseGroupList(ref pointers.PoseKeyframes),
                HeightKeyframes = ReadHeightChangeList(ref pointers.HeightKeyframes),
                NoteKeyframes = ReadNoteEventList(ref pointers.NoteKeyframes),
                ScoreKeyframes = ReadScoreEventList(ref pointers.ScoreKeyframes),
                ComboKeyframes = ReadComboEventList(ref pointers.ComboKeyframes),
                MultiplierKeyframes = ReadMultiplierEventList(ref pointers.MultiplierKeyframes),
                EnergyKeyframes = ReadEnergyEventList(ref pointers.EnergyKeyframes)
            };
            return file;
        }

        private Pointers ReadPointers() {
            int offset = 0;
            return new Pointers {
                Metadata = ReadInt(ref offset),
                PoseKeyframes = ReadInt(ref offset),
                HeightKeyframes = ReadInt(ref offset),
                NoteKeyframes = ReadInt(ref offset),
                ScoreKeyframes = ReadInt(ref offset),
                ComboKeyframes = ReadInt(ref offset),
                MultiplierKeyframes = ReadInt(ref offset),
                EnergyKeyframes = ReadInt(ref offset),
                FPSKeyframes = ReadInt(ref offset)
            };
        }

        private Metadata ReadMetadata(ref int offset) {
            return new Metadata {
                Version = ReadString(ref offset),
                LevelID = ReadString(ref offset),
                Difficulty = ReadInt(ref offset),
                Characteristic = ReadString(ref offset),
                Environment = ReadString(ref offset),
                Modifiers = ReadStringArray(ref offset),
                NoteSpawnOffset = ReadFloat(ref offset),
                LeftHanded = ReadBool(ref offset),
                InitialHeight = ReadFloat(ref offset),
                RoomRotation = ReadFloat(ref offset),
                RoomCenter = ReadVRPosition(ref offset),
                FailTime = ReadFloat(ref offset)
            };
        }

        private VRPoseGroup ReadVRPoseGroup(ref int offset) {
            return new VRPoseGroup {
                Head = ReadVRPose(ref offset),
                Left = ReadVRPose(ref offset),
                Right = ReadVRPose(ref offset),
                FPS = ReadInt(ref offset),
                Time = ReadFloat(ref offset)
            };
        }

        private VRPose ReadVRPose(ref int offset) {
            return new VRPose {
                Position = ReadVRPosition(ref offset),
                Rotation = ReadVRRotation(ref offset)
            };
        }

        private NoteEvent ReadNoteEvent(ref int offset) {
            return new NoteEvent {
                NoteID = ReadNoteID(ref offset),
                EventType = (NoteEventType)ReadInt(ref offset),
                CutPoint = ReadVRPosition(ref offset),
                CutNormal = ReadVRPosition(ref offset),
                SaberDirection = ReadVRPosition(ref offset),
                SaberType = ReadInt(ref offset),
                DirectionOk = ReadBool(ref offset),
                SaberSpeed = ReadFloat(ref offset),
                CutAngle = ReadFloat(ref offset),
                CutDistanceToCenter = ReadFloat(ref offset),
                CutDirectionDeviation = ReadFloat(ref offset),
                BeforeCutRating = ReadFloat(ref offset),
                AfterCutRating = ReadFloat(ref offset),
                Time = ReadFloat(ref offset),
                UnityTimescale = ReadFloat(ref offset),
                TimeSyncTimescale = ReadFloat(ref offset)
            };
        }

        private NoteID ReadNoteID(ref int offset) {
            return new NoteID {
                Time = ReadFloat(ref offset),
                LineLayer = ReadInt(ref offset),
                LineIndex = ReadInt(ref offset),
                ColorType = ReadInt(ref offset),
                CutDirection = ReadInt(ref offset)
            };
        }

        private HeightEvent ReadHeightChange(ref int offset) {
            return new HeightEvent {
                Height = ReadFloat(ref offset),
                Time = ReadFloat(ref offset)
            };
        }

        private ScoreEvent ReadScoreEvent(ref int offset) {
            return new ScoreEvent {
                Score = ReadInt(ref offset),
                Time = ReadFloat(ref offset)
            };
        }

        private ComboEvent ReadComboEvent(ref int offset) {
            return new ComboEvent {
                Combo = ReadInt(ref offset),
                Time = ReadFloat(ref offset)
            };
        }

        private MultiplierEvent ReadMultiplierEvent(ref int offset) {
            return new MultiplierEvent {
                Multiplier = ReadInt(ref offset),
                NextMultiplierProgress = ReadFloat(ref offset),
                Time = ReadFloat(ref offset)
            };
        }

        private EnergyEvent ReadEnergyEvent(ref int offset) {
            return new EnergyEvent {
                Energy = ReadFloat(ref offset),
                Time = ReadFloat(ref offset)
            };
        }

        // Lists
        private string[] ReadStringArray(ref int offset) {
            int size = ReadInt(ref offset);
            string[] value = new string[size];
            for (int i = 0; i < size; i++) {
                value[i] = ReadString(ref offset);
            }

            return value;
        }

        private List<VRPoseGroup> ReadPoseGroupList(ref int offset) {
            int size = ReadInt(ref offset);
            List<VRPoseGroup> values = new List<VRPoseGroup>();
            for (int i = 0; i < size; i++) {
                values.Add(ReadVRPoseGroup(ref offset));
            }

            return values;
        }

        private List<HeightEvent> ReadHeightChangeList(ref int offset) {
            int size = ReadInt(ref offset);
            List<HeightEvent> values = new List<HeightEvent>();
            for (int i = 0; i < size; i++) {
                values.Add(ReadHeightChange(ref offset));
            }

            return values;
        }

        private List<NoteEvent> ReadNoteEventList(ref int offset) {
            int size = ReadInt(ref offset);
            List<NoteEvent> values = new List<NoteEvent>();
            for (int i = 0; i < size; i++) {
                values.Add(ReadNoteEvent(ref offset));
            }

            return values;
        }

        private List<ScoreEvent> ReadScoreEventList(ref int offset) {
            int size = ReadInt(ref offset);
            List<ScoreEvent> values = new List<ScoreEvent>();
            for (int i = 0; i < size; i++) {
                values.Add(ReadScoreEvent(ref offset));
            }

            return values;
        }

        private List<ComboEvent> ReadComboEventList(ref int offset) {
            int size = ReadInt(ref offset);
            List<ComboEvent> values = new List<ComboEvent>();
            for (int i = 0; i < size; i++) {
                values.Add(ReadComboEvent(ref offset));
            }

            return values;
        }

        private List<MultiplierEvent> ReadMultiplierEventList(ref int offset) {
            int size = ReadInt(ref offset);
            List<MultiplierEvent> values = new List<MultiplierEvent>();
            for (int i = 0; i < size; i++) {
                values.Add(ReadMultiplierEvent(ref offset));
            }

            return values;
        }

        private List<EnergyEvent> ReadEnergyEventList(ref int offset) {
            int size = ReadInt(ref offset);
            List<EnergyEvent> values = new List<EnergyEvent>();
            for (int i = 0; i < size; i++) {
                values.Add(ReadEnergyEvent(ref offset));
            }

            return values;
        }

        // Primitives
        private string ReadString(ref int offset) {
            int stringLength = BitConverter.ToInt32(_input, offset);
            string value = Encoding.UTF8.GetString(_input, offset + 4, stringLength);
            offset += stringLength + 4;
            return value;
        }

        private int ReadInt(ref int offset) {
            int value = BitConverter.ToInt32(_input, offset);
            offset += 4;
            return value;
        }

        private float ReadFloat(ref int offset) {
            float value = BitConverter.ToSingle(_input, offset);
            offset += 4;
            return value;
        }

        private bool ReadBool(ref int offset) {
            bool value = BitConverter.ToBoolean(_input, offset);
            offset += 1;
            return value;
        }

        private VRPosition ReadVRPosition(ref int offset) {
            return new VRPosition {
                X = ReadFloat(ref offset),
                Y = ReadFloat(ref offset),
                Z = ReadFloat(ref offset)
            };
        }

        private VRRotation ReadVRRotation(ref int offset) {
            return new VRRotation {
                X = ReadFloat(ref offset),
                Y = ReadFloat(ref offset),
                Z = ReadFloat(ref offset),
                W = ReadFloat(ref offset)
            };
        }
    }
}