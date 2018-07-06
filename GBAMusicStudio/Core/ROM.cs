﻿using GBAMusicStudio.Util;
using System.IO;

namespace GBAMusicStudio.Core
{
    internal class ROM : ROMReader
    {
        internal const uint Pak = 0x8000000;
        internal const uint Capacity = 0x2000000;

        internal static ROM Instance { get; private set; } // If you want to read with the reader

        internal readonly byte[] ROMFile;
        internal AGame Game { get; private set; }
        internal SongTable[] SongTables { get; private set; }

        internal ROM(string filePath)
        {
            Instance = this;
            SongPlayer.Stop();
            ROMFile = File.ReadAllBytes(filePath);
            InitReader();
            ReloadGameConfig();
            SongPlayer.Reset();
        }
        internal void ReloadGameConfig()
        {
            Game = Config.Games[System.Text.Encoding.Default.GetString(ReadBytes(4, 0xAC))];
            SongTables = new SongTable[Game.SongTables.Length];
            for (int i = 0; i < Game.SongTables.Length; i++)
            {
                switch (Game.Engine)
                {
                    case AEngine.M4A: SongTables[i] = new M4ASongTable(Game.SongTables[i]); break;
                    case AEngine.MLSS: SongTables[i] = new MLSSSongTable(Game.SongTables[i]); break;
                }
            }
        }

        internal T ReadStruct<T>(uint offset = 0xFFFFFFFF)
        {
            if (IsValidRomOffset(offset))
                SetOffset(offset);
            return Utils.ReadStruct<T>(ROMFile, Position);
        }

        internal static bool IsValidRomOffset(uint offset) => (offset < Capacity && offset < Instance.ROMFile.Length) || (offset >= Pak && offset < Pak + Capacity && offset < Instance.ROMFile.Length + Pak);
    }
}
