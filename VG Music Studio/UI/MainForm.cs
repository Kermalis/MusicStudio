﻿using Kermalis.VGMusicStudio.Core;
using Kermalis.VGMusicStudio.Core.GBA.M4A;
using Kermalis.VGMusicStudio.Core.GBA.MLSS;
using Kermalis.VGMusicStudio.Core.NDS.DSE;
using Kermalis.VGMusicStudio.Core.NDS.SDAT;
using Kermalis.VGMusicStudio.Properties;
using Kermalis.VGMusicStudio.Util;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Kermalis.VGMusicStudio.UI
{
    [DesignerCategory("")]
    internal class MainForm : ThemedForm
    {
        public static MainForm Instance { get; } = new MainForm();

        private bool stopUI = false;
        private readonly List<byte> pianoNotes = new List<byte>();
        public readonly bool[] PianoTracks = new bool[0x10];

        private const int iWidth = 528, iHeight = 800 + 25; // +25 for menustrip (24) and splitcontainer separator (1)
        private const float sfWidth = 2.35f; // Song combobox and volumebar width
        private const float spfHeight = 5.5f; // Split panel 1 height

        #region Controls

        private readonly IContainer components;
        private readonly MenuStrip mainMenu;
        private readonly ToolStripMenuItem fileItem, openDSEItem, openM4AItem, openMLSSItem, openSDATItem;
        private readonly Timer timer;
        private readonly ThemedNumeric songNumerical;
        private readonly ThemedButton playButton, pauseButton, stopButton;
        private readonly SplitContainer splitContainer;
        private readonly PianoControl piano;
        private readonly ColorSlider volumeBar;
        private readonly TrackInfoControl trackInfo;
        private readonly ImageComboBox songsComboBox;
        private readonly ThumbnailToolBarButton prevTButton, toggleTButton, nextTButton;

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
        private MainForm()
        {
            for (int i = 0; i < PianoTracks.Length; i++)
            {
                PianoTracks[i] = true;
            }

            components = new Container();

            // Main Menu
            openDSEItem = new ToolStripMenuItem { Text = "Open DSE Folder" };
            openDSEItem.Click += OpenDSE;

            openM4AItem = new ToolStripMenuItem { Text = "Open GBA ROM (M4A/MP2K)" };
            openM4AItem.Click += OpenM4A;

            openMLSSItem = new ToolStripMenuItem { Text = "Open GBA ROM (MLSS)" };
            openMLSSItem.Click += OpenMLSS;

            openSDATItem = new ToolStripMenuItem { Text = "Open SDAT File" };
            openSDATItem.Click += OpenSDAT;

            fileItem = new ToolStripMenuItem { Text = Strings.MenuFile };
            fileItem.DropDownItems.AddRange(new ToolStripItem[] { openDSEItem, openM4AItem, openMLSSItem, openSDATItem });


            mainMenu = new MenuStrip { Size = new Size(iWidth, 24) };
            mainMenu.Items.AddRange(new ToolStripItem[] { fileItem });

            // Buttons
            playButton = new ThemedButton { ForeColor = Color.MediumSpringGreen, Location = new Point(5, 3), Text = Strings.PlayerPlay };
            playButton.Click += (o, e) => Play();
            pauseButton = new ThemedButton { ForeColor = Color.DeepSkyBlue, Location = new Point(85, 3), Text = Strings.PlayerPause };
            pauseButton.Click += (o, e) => Pause();
            stopButton = new ThemedButton { ForeColor = Color.MediumVioletRed, Location = new Point(166, 3), Text = Strings.PlayerStop };
            stopButton.Click += (o, e) => Stop();

            playButton.Enabled = pauseButton.Enabled = stopButton.Enabled = false;
            playButton.Size = stopButton.Size = new Size(75, 23);
            pauseButton.Size = new Size(76, 23);

            // Numericals
            songNumerical = new ThemedNumeric { Enabled = false, Location = new Point(246, 4), Minimum = ushort.MinValue, Visible = false };

            songNumerical.Size = new Size(45, 23);
            songNumerical.ValueChanged += SongNumerical_ValueChanged;

            // Timer
            timer = new Timer(components);
            timer.Tick += UpdateUI;

            // Piano
            piano = new PianoControl { Anchor = AnchorStyles.Bottom, Location = new Point(0, 125 - 50 - 1), Size = new Size(iWidth, 50) };

            // Volume bar
            int sWidth = (int)(iWidth / sfWidth);
            int sX = iWidth - sWidth - 4;
            volumeBar = new ColorSlider
            {
                Enabled = false,
                LargeChange = 20,
                Location = new Point(83, 45),
                Maximum = 100,
                Size = new Size(155, 27),
                SmallChange = 5
            };
            volumeBar.ValueChanged += VolumeBar_ValueChanged;

            // Playlist box
            songsComboBox = new ImageComboBox
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Enabled = false,
                Location = new Point(sX, 4),
                Size = new Size(sWidth, 23)
            };
            songsComboBox.SelectedIndexChanged += SongsComboBox_SelectedIndexChanged;

            // Track info
            trackInfo = new TrackInfoControl
            {
                Dock = DockStyle.Fill,
                Size = new Size(iWidth, 690)
            };

            // Split container
            splitContainer = new SplitContainer
            {
                BackColor = Theme.TitleBar,
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                IsSplitterFixed = true,
                Orientation = Orientation.Horizontal,
                Size = new Size(iWidth, iHeight),
                SplitterDistance = 125,
                SplitterWidth = 1
            };
            splitContainer.Panel1.Controls.AddRange(new Control[] { playButton, pauseButton, stopButton, songNumerical, songsComboBox, piano, volumeBar });
            splitContainer.Panel2.Controls.Add(trackInfo);

            // MainForm
            AutoScaleDimensions = new SizeF(6, 13);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(iWidth, iHeight);
            Controls.AddRange(new Control[] { splitContainer, mainMenu });
            MainMenuStrip = mainMenu;
            MinimumSize = new Size(8 + iWidth + 8, 30 + iHeight + 8); // Borders
            Resize += OnResize;
            Text = Utils.ProgramName;

            // Taskbar Buttons
            if (TaskbarManager.IsPlatformSupported)
            {
                prevTButton = new ThumbnailToolBarButton(Resources.IconPrevious, Strings.PlayerPreviousSong);
                prevTButton.Click += (o, e) => PlayPreviousSong();
                toggleTButton = new ThumbnailToolBarButton(Resources.IconPlay, Strings.PlayerPlay);
                toggleTButton.Click += (o, e) => TogglePlayback();
                nextTButton = new ThumbnailToolBarButton(Resources.IconNext, Strings.PlayerNextSong);
                nextTButton.Click += (o, e) => PlayNextSong();
                prevTButton.Enabled = toggleTButton.Enabled = nextTButton.Enabled = false;
                TaskbarManager.Instance.ThumbnailToolBars.AddButtons(Handle, prevTButton, toggleTButton, nextTButton);
            }
        }

        private void VolumeBar_ValueChanged(object sender, EventArgs e)
        {
            Engine.Instance.Mixer.SetVolume(volumeBar.Value / (float)volumeBar.Maximum);
        }
        public void SetVolumeBarValue(float volume)
        {
            volumeBar.ValueChanged -= VolumeBar_ValueChanged;
            volumeBar.Value = (int)(volume * volumeBar.Maximum);
            volumeBar.ValueChanged += VolumeBar_ValueChanged;
        }

        private bool autoplay;
        private void SongNumerical_ValueChanged(object sender, EventArgs e)
        {
            songsComboBox.SelectedIndexChanged -= SongsComboBox_SelectedIndexChanged;

            long index = (long)songNumerical.Value;
            Stop();
            try
            {
                Engine.Instance.Player.LoadSong(index);
                Config config = Engine.Instance.Config;
                List<Config.Song> songs = config.Playlists[0].Songs; // Complete "Music" playlist is present in all configs at index 0
                Config.Song song = songs.SingleOrDefault(s => s.Index == index);
                if (song != null)
                {
                    Text = $"{Utils.ProgramName} - {song.Name}";
                    songsComboBox.SelectedIndex = songs.IndexOf(song) + 1; // + 1 because the "Music" playlist is first in the combobox
                }
                else
                {
                    Text = Utils.ProgramName;
                    songsComboBox.SelectedIndex = 0;
                }
                trackInfo.DeleteData();
                if (autoplay)
                {
                    Play();
                }
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(ex.Message, string.Format(Strings.ErrorLoadSong, songNumerical.Value));
            }

            autoplay = true;
            songsComboBox.SelectedIndexChanged += SongsComboBox_SelectedIndexChanged;
        }
        private void SongsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = (ImageComboBoxItem)songsComboBox.SelectedItem;
            if (item.Item is Config.Song song)
            {
                songNumerical.Value = song.Index;
            }
        }

        private void OpenDSE(object sender, EventArgs e)
        {
            var d = new CommonOpenFileDialog
            {
                Title = "Open DSE Folder",
                IsFolderPicker = true
            };
            if (d.ShowDialog() == CommonFileDialogResult.Ok)
            {
                try
                {
                    EngineShutDown();
                    new Engine(Engine.EngineType.NDS_DSE, d.FileName);
                    var config = (DSEConfig)Engine.Instance.Config;
                    FinishLoading(false, config.BGMFiles.Length);
                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show(ex.Message, "Error Loading DSE");
                }
            }
        }
        private void OpenM4A(object sender, EventArgs e)
        {
            var d = new CommonOpenFileDialog
            {
                Title = "Open GBA ROM (M4A/MP2K)",
                Filters = { new CommonFileDialogFilter("GBA Files", ".gba") }
            };
            if (d.ShowDialog() == CommonFileDialogResult.Ok)
            {
                try
                {
                    EngineShutDown();
                    new Engine(Engine.EngineType.GBA_M4A, File.ReadAllBytes(d.FileName));
                    var config = (M4AConfig)Engine.Instance.Config;
                    FinishLoading(true, config.SongTableSizes[0]);
                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show(ex.Message, "Error Loading GBA ROM (M4A/MP2K)");
                }
            }
        }
        private void OpenMLSS(object sender, EventArgs e)
        {
            var d = new CommonOpenFileDialog
            {
                Title = "Open GBA ROM (M4A/MP2K)",
                Filters = { new CommonFileDialogFilter("GBA Files", ".gba") }
            };
            if (d.ShowDialog() == CommonFileDialogResult.Ok)
            {
                try
                {
                    EngineShutDown();
                    new Engine(Engine.EngineType.GBA_MLSS, File.ReadAllBytes(d.FileName));
                    var config = (MLSSConfig)Engine.Instance.Config;
                    FinishLoading(true, config.SongTableSizes[0]);
                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show(ex.Message, "Error Loading GBA ROM (MLSS)");
                }
            }
        }
        private void OpenSDAT(object sender, EventArgs e)
        {
            var d = new CommonOpenFileDialog
            {
                Title = "Open SDAT File",
                Filters = { new CommonFileDialogFilter("SDAT Files", ".sdat") }
            };
            if (d.ShowDialog() == CommonFileDialogResult.Ok)
            {
                try
                {
                    EngineShutDown();
                    var sdat = new SDAT(File.ReadAllBytes(d.FileName));
                    new Engine(Engine.EngineType.NDS_SDAT, sdat);
                    FinishLoading(true, sdat.INFOBlock.SequenceInfos.NumEntries);
                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show(ex.Message, "Error Loading SDAT File");
                }
            }
        }

        private void Play()
        {
            Engine.Instance.Player.Play();
            pauseButton.Enabled = stopButton.Enabled = true;
            pauseButton.Text = Strings.PlayerPause;
            timer.Interval = (int)(1000.0 / GlobalConfig.Instance.RefreshRate);
            timer.Start();
            UpdateTaskbarButtons();
        }
        private void Pause()
        {
            Engine.Instance.Player.Pause();
            if (Engine.Instance.Player.State != PlayerState.Paused)
            {
                stopButton.Enabled = true;
                pauseButton.Text = Strings.PlayerPause;
                timer.Start();
            }
            else
            {
                stopButton.Enabled = false;
                pauseButton.Text = Strings.PlayerUnpause;
                timer.Stop();
                System.Threading.Monitor.Enter(timer);
                ClearPianoNotes();
            }
            UpdateTaskbarButtons();
        }
        private void Stop()
        {
            Engine.Instance.Player.Stop();
            pauseButton.Enabled = stopButton.Enabled = false;
            timer.Stop();
            System.Threading.Monitor.Enter(timer);
            ClearPianoNotes();
            trackInfo.DeleteData();
            UpdateTaskbarButtons();
        }
        private void TogglePlayback()
        {
            if (Engine.Instance.Player.State == PlayerState.Stopped)
            {
                Play();
            }
            else if (Engine.Instance.Player.State == PlayerState.Paused || Engine.Instance.Player.State == PlayerState.Playing)
            {
                Pause();
            }
        }
        private void PlayPreviousSong()
        {
            if (songNumerical.Value > 0)
            {
                songNumerical.Value--;
            }
        }
        private void PlayNextSong()
        {
            if (songNumerical.Value < songNumerical.Maximum)
            {
                songNumerical.Value++;
            }
        }

        private void FinishLoading(bool numericalVisible, long numSongs)
        {
            autoplay = false;
            Engine.Instance.Player.SongEnded += SongEnded;
            foreach (Config.Playlist playlist in Engine.Instance.Config.Playlists)
            {
                songsComboBox.Items.Add(new ImageComboBoxItem(playlist, Resources.IconPlaylist, 0));
                songsComboBox.Items.AddRange(playlist.Songs.Select(s => new ImageComboBoxItem(s, Resources.IconSong, 1)).ToArray());
            }
            long landingSong = Engine.Instance.Config.Playlists[0].Songs.Count == 0 ? 0 : Engine.Instance.Config.Playlists[0].Songs[0].Index;
            songNumerical.Visible = numericalVisible;
            songNumerical.Maximum = numSongs - 1;
            if (songNumerical.Value == landingSong)
            {
                SongNumerical_ValueChanged(null, null);
            }
            else
            {
                songNumerical.Value = landingSong;
            }
            songsComboBox.Enabled = songNumerical.Enabled = playButton.Enabled = volumeBar.Enabled = true;
            UpdateTaskbarButtons();
        }
        private void EngineShutDown()
        {
            if (Engine.Instance != null)
            {
                Stop();
                Engine.Instance.ShutDown();
            }
            prevTButton.Enabled = toggleTButton.Enabled = nextTButton.Enabled = songsComboBox.Enabled = songNumerical.Enabled = playButton.Enabled = volumeBar.Enabled = false;
            Text = Utils.ProgramName;
            songsComboBox.SelectedIndexChanged -= SongsComboBox_SelectedIndexChanged;
            songNumerical.ValueChanged -= SongNumerical_ValueChanged;
            songNumerical.Visible = false;
            songNumerical.Value = 0;
            songsComboBox.SelectedItem = null;
            songsComboBox.Items.Clear();
            songsComboBox.SelectedIndexChanged += SongsComboBox_SelectedIndexChanged;
            songNumerical.ValueChanged += SongNumerical_ValueChanged;
        }
        private void ClearPianoNotes()
        {
            foreach (byte n in pianoNotes)
            {
                if (n >= piano.LowNoteID && n <= piano.HighNoteID)
                {
                    piano[n - piano.LowNoteID].NoteOnColor = Color.DeepSkyBlue;
                    piano.ReleasePianoKey(n);
                }
            }
            pianoNotes.Clear();
        }
        private void UpdateUI(object sender, EventArgs e)
        {
            if (!System.Threading.Monitor.TryEnter(timer))
            {
                return;
            }
            try
            {
                // Song ended
                if (stopUI)
                {
                    stopUI = false;
                    Stop();
                }
                // Draw
                else
                {
                    // Draw piano notes
                    ClearPianoNotes();
                    TrackInfoControl.TrackInfo info = trackInfo.Info;
                    Engine.Instance.Player.GetSongState(info);
                    for (int i = PianoTracks.Length - 1; i >= 0; i--)
                    {
                        if (!PianoTracks[i])
                        {
                            continue;
                        }

                        byte[] notes = info.Notes[i];
                        pianoNotes.AddRange(notes);
                        foreach (byte n in notes)
                        {
                            if (n >= piano.LowNoteID && n <= piano.HighNoteID)
                            {
                                piano[n - piano.LowNoteID].NoteOnColor = GlobalConfig.Instance.Colors[info.Voices[i]];
                                piano.PressPianoKey(n);
                            }
                        }
                    }
                    // Draw trackinfo
                    trackInfo.Invalidate();
                }
            }
            finally
            {
                System.Threading.Monitor.Exit(timer);
            }
        }
        private void SongEnded()
        {
            stopUI = true;
        }

        private void UpdateTaskbarButtons()
        {
            if (TaskbarManager.IsPlatformSupported)
            {
                prevTButton.Enabled = songNumerical.Value > 0;
                nextTButton.Enabled = songNumerical.Value < songNumerical.Maximum;
                switch (Engine.Instance.Player.State)
                {
                    case PlayerState.Stopped: toggleTButton.Icon = Resources.IconPlay; toggleTButton.Tooltip = Strings.PlayerPlay; break;
                    case PlayerState.Playing: toggleTButton.Icon = Resources.IconPause; toggleTButton.Tooltip = Strings.PlayerPause; break;
                    case PlayerState.Paused: toggleTButton.Icon = Resources.IconPlay; toggleTButton.Tooltip = Strings.PlayerUnpause; break;
                }
                toggleTButton.Enabled = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            EngineShutDown();
            base.OnFormClosing(e);
        }
        private void OnResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                return;
            }

            // Song combobox
            int sWidth = (int)(splitContainer.Width / sfWidth);
            int sX = splitContainer.Width - sWidth - 4;
            songsComboBox.Location = new Point(sX, 4);
            songsComboBox.Size = new Size(sWidth, 23);

            splitContainer.SplitterDistance = (int)((Height - 38) / spfHeight) - 24 - 1;

            // Piano
            piano.Size = new Size(splitContainer.Width, (int)(splitContainer.Panel1.Height / 2.5f)); // Force it to initialize piano keys again
            int targetWhites = piano.Width / 10; // Minimum width of a white key is 10 pixels
            int targetAmount = (targetWhites / 7 * 12).Clamp(1, 128); // 7 white keys per octave
            int offset = (targetAmount / 2) - (targetWhites / 7 % 2);
            piano.LowNoteID = Math.Max(0, 60 - offset);
            piano.HighNoteID = (60 + offset - 1) >= 120 ? 127 : (60 + offset - 1);

            int wWidth = piano[0].Width; // White key width
            int dif = splitContainer.Width - (wWidth * piano.WhiteKeyCount);
            piano.Location = new Point(dif / 2, splitContainer.Panel1.Height - piano.Height - 1);
            piano.Invalidate(true);
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (playButton.Enabled && !songsComboBox.Focused && keyData == Keys.Space)
            {
                if (Engine.Instance.Player.State == PlayerState.Stopped)
                {
                    Play();
                }
                else
                {
                    Pause();
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}