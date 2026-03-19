// Imports
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.IO;              // for Path.Combine
using System.Drawing;         // for Bitmap/PictureBox
using System.Windows.Forms;   // ensure WinForms
using System.Runtime.InteropServices;
using NAudio.Wave;      // for audio device enumeration.  not capture
using SpinnakerNET;     // for FLIR camera enumeration. not capture
using FLIRcapture_2Ch.Devices;
using FLIRcapture_2Ch.Utilities;

// Start of Code
namespace FLIRcapture_2Ch
{
    public partial class Form1 : Form
    {
        // These are variables to be declared for the form in UI. Insert new ones below. 
        private ComboBox cmbCamera1, cmbCamera2, cmbMic1, cmbMic2; // Dropdowns for 2 channels
        private PictureBox previewBox1, previewBox2; // Preview boxes for 2 channels
        private Button btnStart1, btnStop1, btnStart2, btnStop2; // Start/Stop buttons for 2 channels
        private TextBox textBoxVideoPath1, textBoxAudioPath1, textBoxVideoPath2, textBoxAudioPath2; //wait, shouldn't these be txtbox with browse function?
        private Utilities.RecChannel _ch1;
        private Utilities.RecChannel _ch2;

        // Hotkey constants
        private const int HOTKEY_ID = 1;          // Arbitrary ID for this hotkey
        private const uint MOD_CONTROL = 0x0002;  // Ctrl
        private const uint MOD_ALT = 0x0001;      // Alt
        private const uint VK_Q = 0x51;           // Virtual key code for 'Q'
        
        private void UpdateStartButtons() // keeping Start button state active


        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        public Form1()
        {
            // this describes what happens when the form is initialized.
            InitializeComponent();
            InitializeUI();
            this.Load += new EventHandler(Form1_Load);
            // Register Ctrl+Alt+Q as a global hotkey for code termination.
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_Q);
        }
        private void InitializeUI()
        {
            // === Form properties ===
            this.Text = "FLIR Dual-Channel Recorder";
            this.ClientSize = new Size(1100, 600);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // === Channel 1 controls ===
            var lblCam1 = new Label { Text = "Channel 1 Camera:", Location = new Point(20, 20), AutoSize = true };
            cmbCamera1 = new ComboBox { Location = new Point(140, 20), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };

            var lblMic1 = new Label { Text = "Channel 1 Mic:", Location = new Point(20, 60), AutoSize = true };
            cmbMic1 = new ComboBox { Location = new Point(140, 60), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };

            previewBox1 = new PictureBox
            {
                Location = new Point(20, 100),
                Size = new Size(480, 270),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            btnStart1 = new Button { Text = "Start 1", Location = new Point(50, 400), Size = new Size(100, 40) };
            btnStart1.Click += BtnStart1_Click;

            btnStop1 = new Button { Text = "Stop 1", Location = new Point(190, 400), Size = new Size(100, 40) };
            btnStop1.Click += (s, e) => StopRecordingChannel1();

            // === Channel 2 controls ===
            var lblCam2 = new Label { Text = "Channel 2 Camera:", Location = new Point(560, 20), AutoSize = true };
            cmbCamera2 = new ComboBox { Location = new Point(680, 20), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };

            var lblMic2 = new Label { Text = "Channel 2 Mic:", Location = new Point(560, 60), AutoSize = true };
            cmbMic2 = new ComboBox { Location = new Point(680, 60), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };

            previewBox2 = new PictureBox
            {
                Location = new Point(560, 100),
                Size = new Size(480, 270),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            btnStart2 = new Button { Text = "Start 2", Location = new Point(590, 400), Size = new Size(100, 40) };
            btnStart2.Click += BtnStart2_Click;

            btnStop2 = new Button { Text = "Stop 2", Location = new Point(730, 400), Size = new Size(100, 40) };
            btnStop2.Click += (s, e) => StopRecordingChannel2();

            // --- Channel 1 paths ---
            var lblVid1 = new Label { Text = "Video Path 1:", Location = new Point(20, 380), AutoSize = true };
            textBoxVideoPath1 = new TextBox { Location = new Point(140, 375), Width = 360 };

            var lblAud1 = new Label { Text = "Audio Path 1:", Location = new Point(20, 430), AutoSize = true };
            textBoxAudioPath1 = new TextBox { Location = new Point(140, 425), Width = 360 };

            // --- Channel 2 paths ---
            var lblVid2 = new Label { Text = "Video Path 2:", Location = new Point(560, 380), AutoSize = true };
            textBoxVideoPath2 = new TextBox { Location = new Point(680, 375), Width = 360 };

            var lblAud2 = new Label { Text = "Audio Path 2:", Location = new Point(560, 430), AutoSize = true };
            textBoxAudioPath2 = new TextBox { Location = new Point(680, 425), Width = 360 };

            // === Add controls to the form ===
            Controls.AddRange(new Control[] {
                lblCam1, cmbCamera1, lblMic1, cmbMic1, previewBox1, btnStart1, btnStop1,
                lblCam2, cmbCamera2, lblMic2, cmbMic2, previewBox2, btnStart2, btnStop2,
                lblVid1, textBoxVideoPath1, lblAud1, textBoxAudioPath1,
                lblVid2, textBoxVideoPath2, lblAud2, textBoxAudioPath2
            });
            cmbCamera1.SelectedIndexChanged += (s, e) => UpdateStartButtons();
            cmbMic1.SelectedIndexChanged += (s, e) => UpdateStartButtons();
            cmbCamera2.SelectedIndexChanged += (s, e) => UpdateStartButtons();
            cmbMic2.SelectedIndexChanged += (s, e) => UpdateStartButtons();
        }

        private class CameraListItem
        {
            public string Id { get; set; }       // index as string ("0", "1")
            public string Display { get; set; }  // e.g., "[0] FLIR <serial or model>"
            public override string ToString() => Display;
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                // --- Mics ---
                cmbMic1.Items.Clear();
                cmbMic2.Items.Clear();
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var caps = WaveIn.GetCapabilities(i);
                    string name = $"{i}: {caps.ProductName}";
                    cmbMic1.Items.Add(name);
                    cmbMic2.Items.Add(name);
                }

                // --- Cameras (build by index with friendly labels) ---
                var camItems = new List<CameraListItem>();
                using (var system = SpinnakerNET.ManagedSystem.GetInstance()) // or new ManagedSystem();
                using (var list = system.GetCameras())
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        using (var cam = list[i])
                        {
                            string serial = string.Empty;
                            try
                            {
                                cam.Init();
                                serial = cam.TLDevice.DeviceSerialNumber?.ToString();
                            }
                            catch { /* ignore */ }
                            finally
                            {
                                try { cam.DeInit(); } catch { /* ignore */ }
                            }

                            camItems.Add(new CameraListItem
                            {
                                Id = i.ToString(),
                                Display = !string.IsNullOrWhiteSpace(serial)
                                        ? $"[{i}] FLIR SN {serial}"
                                        : $"[{i}] FLIR Camera"
                            });
                        }
                    }
                }

                cmbCamera1.DataSource = camItems;
                cmbCamera1.DisplayMember = nameof(CameraListItem.Display);
                cmbCamera1.ValueMember = nameof(CameraListItem.Id);

                cmbCamera2.DataSource = new List<CameraListItem>(camItems);
                cmbCamera2.DisplayMember = nameof(CameraListItem.Display);
                cmbCamera2.ValueMember = nameof(CameraListItem.Id);

                // --- Defaults ---
                if (cmbCamera1.Items.Count > 0) cmbCamera1.SelectedIndex = 0;
                if (cmbMic1.Items.Count > 0) cmbMic1.SelectedIndex = 0;
                if (cmbCamera2.Items.Count > 1) cmbCamera2.SelectedIndex = 1;
                if (cmbMic2.Items.Count > 1) cmbMic2.SelectedIndex = 1;

                // Default save paths
                textBoxVideoPath1.Text = Path.Combine(Config.Paths.OutputDirectory, "channel1.mp4");
                textBoxAudioPath1.Text = Path.Combine(Config.Paths.OutputDirectory, "channel1.wav");
                textBoxVideoPath2.Text = Path.Combine(Config.Paths.OutputDirectory, "channel2.mp4");
                textBoxAudioPath2.Text = Path.Combine(Config.Paths.OutputDirectory, "channel2.wav");

                UpdateStartButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Device enumeration failed:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void BtnStart1_Click(object sender, EventArgs e)
        {
            if (_ch1 != null) { MessageBox.Show("Channel 1 already running."); return; }
            if (cmbCamera1.SelectedItem is not CameraListItem camItem)
            {
                MessageBox.Show("Please select a camera for Channel 1."); return;
            }
            if (cmbMic1.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a mic for Channel 1."); return;
            }

            _ch1 = new Utilities.RecChannel(
                cameraIdOrIndex: camItem.Id,                // "0" or "1"
                micIndex: cmbMic1.SelectedIndex,
                videoPath: textBoxVideoPath1.Text,
                audioPath: textBoxAudioPath1.Text,
                onFrameReady: bmp =>
                {
                    if (previewBox1.IsHandleCreated && !previewBox1.IsDisposed)
                    {
                        previewBox1.BeginInvoke((Action)(() =>
                        {
                            var old = previewBox1.Image;
                            previewBox1.Image = bmp;   // UI now owns bmp
                            old?.Dispose();            // avoid GDI leaks
                        }));
                    }
                    else
                    {
                        bmp?.Dispose();
                    }
                },
                onError: msg => this.BeginInvoke((Action)(() =>
                {
                    MessageBox.Show(this, msg, "Channel 1", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                })),
                enableAudioCsv: true
            );

            try
            {
                _ch1.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Channel 1 start failed:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _ch1?.Dispose();
                _ch1 = null;
            }

            UpdateStartButtons();
        }

        private void BtnStart2_Click(object sender, EventArgs e)
        {
            if (_ch2 != null) { MessageBox.Show("Channel 2 already running."); return; }
            if (cmbCamera2.SelectedItem is not CameraListItem camItem)
            {
                MessageBox.Show("Please select a camera for Channel 2."); return;
            }
            if (cmbMic2.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a mic for Channel 2."); return;
            }

            _ch2 = new Utilities.RecChannel(
                cameraIdOrIndex: camItem.Id,
                micIndex: cmbMic2.SelectedIndex,
                videoPath: textBoxVideoPath2.Text,
                audioPath: textBoxAudioPath2.Text,
                onFrameReady: bmp =>
                {
                    if (previewBox2.IsHandleCreated && !previewBox2.IsDisposed)
                    {
                        previewBox2.BeginInvoke((Action)(() =>
                        {
                            var old = previewBox2.Image;
                            previewBox2.Image = bmp;
                            old?.Dispose();
                        }));
                    }
                    else
                    {
                        bmp?.Dispose();
                    }
                },
                onError: msg => this.BeginInvoke((Action)(() =>
                {
                    MessageBox.Show(this, msg, "Channel 2", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                })),
                enableAudioCsv: true
            );

            try
            {
                _ch2.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Channel 2 start failed:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _ch2?.Dispose();
                _ch2 = null;
            }

            UpdateStartButtons();
        }

        private void StopRecordingChannel1()
        {
            try { _ch1?.Stop(); } catch { }
            try { _ch1?.Dispose(); } catch { }
            _ch1 = null;

            if (previewBox1.Image != null)
            {
                var old = previewBox1.Image;
                previewBox1.Image = null;
                old.Dispose();
            }
            UpdateStartButtons();
        }

        private void StopRecordingChannel2()
        {
            try { _ch2?.Stop(); } catch { }
            try { _ch2?.Dispose(); } catch { }
            _ch2 = null;

            if (previewBox2.Image != null)
            {
                var old = previewBox2.Image;
                previewBox2.Image = null;
                old.Dispose();
            }
            UpdateStartButtons();
        }

        private void StopRecordingChannel1()
        {
            try { _ch1?.Stop(); } catch { }
            try { _ch1?.Dispose(); } catch { }
            _ch1 = null;

            if (previewBox1.Image != null)
            {
                var old = previewBox1.Image;
                previewBox1.Image = null;
                old.Dispose();
            }
            UpdateStartButtons();
        }

        private void StopRecordingChannel2()
        {
            try { _ch2?.Stop(); } catch { }
            try { _ch2?.Dispose(); } catch { }
            _ch2 = null;

            if (previewBox2.Image != null)
            {
                var old = previewBox2.Image;
                previewBox2.Image = null;
                old.Dispose();
            }
            UpdateStartButtons();
        }

        protected override void WndProc(ref Message m)
        {

            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                // Custom method to stop both channels
                StopRecordingChannel1();
                StopRecordingChannel2();
            }
            base.WndProc(ref m);
        }
    }

}