// Imports
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.IO;              // for Path.Combine
using System.Drawing;         // for Bitmap/PictureBox
using System.Runtime.InteropServices;
using NAudio.Wave;      // for audio device enumeration.  not capture
using SpinnakerNET;     // for FLIR camera enumeration. not capture
using FLIRcapture_2Ch;
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

        private string inputPath1, inputPath2;
        private Utilities.RecChannel _ch1;
        private Utilities.RecChannel _ch2;

        // Hotkey constants
        private const int HOTKEY_ID = 1;          // Arbitrary ID for this hotkey
        private const uint MOD_CONTROL = 0x0002;  // Ctrl
        private const uint MOD_ALT = 0x0001;      // Alt
        private const uint VK_Q = 0x51;           // Virtual key code for 'Q'


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
            this.ClientSize = new Size(1100, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // === Channel 1 controls ===
            var lblCam1 = new Label { Text = "Channel 1 Camera:", Location = new Point(20, 20), AutoSize = true };
            cmbCamera1 = new ComboBox { Location = new Point(140, 20), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };

            var lblMic1 = new Label { Text = "Channel 1 Mic:", Location = new Point(20, 60), AutoSize = true };
            cmbMic1 = new ComboBox { Location = new Point(140, 60), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };

            previewBox1 = new PictureBox
            {
                Location = new Point(20, 140),
                Size = new Size(480, 270),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            btnStart1 = new Button { Text = "Start 1", Location = new Point(50, 420), Size = new Size(100, 40) };
            btnStart1.Click += BtnStart1_Click;

            btnStop1 = new Button { Text = "Stop 1", Location = new Point(190, 420), Size = new Size(100, 40) };
            btnStop1.Click += (s, e) => StopRecordingChannel1();

            // Add a TextBox + Button for folder path selection Channel 1
            var lblSavePath1 = new Label
            {
                Text = "Save Folder:",
                Location = new System.Drawing.Point(20, 100),
                AutoSize = true
            };

            var txtSavePath1 = new TextBox
            {
                Location = new System.Drawing.Point(140, 100),
                Width = 250,
                ReadOnly = true
            };

            var btnBrowseFolder1 = new Button
            {
                Text = "Browse...",
                Location = new System.Drawing.Point(400, 95),
                Size = new System.Drawing.Size(100, 30)
            };
            btnBrowseFolder1.Click += (s, e) =>
            {
                using (var folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select a folder to save recordings";
                    folderDialog.ShowNewFolderButton = true;

                    // Try to set initial directory to E:\
                    string initialPath = @"D:\test_dump\";
                    if (Directory.Exists(initialPath))
                    {
                        folderDialog.SelectedPath = initialPath;
                    }
                    else
                    {
                        // Fallback to Desktop if D:\ is not available
                        folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    }

                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        txtSavePath1.Text = folderDialog.SelectedPath;
                        inputPath1 = folderDialog.SelectedPath; // store for later use
                    }
                }
            };

            // === Channel 2 controls ===
            var lblCam2 = new Label { Text = "Channel 2 Camera:", Location = new Point(560, 20), AutoSize = true };
            cmbCamera2 = new ComboBox { Location = new Point(680, 20), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };

            var lblMic2 = new Label { Text = "Channel 2 Mic:", Location = new Point(560, 60), AutoSize = true };
            cmbMic2 = new ComboBox { Location = new Point(680, 60), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };

            previewBox2 = new PictureBox
            {
                Location = new Point(560, 140),
                Size = new Size(480, 270),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            btnStart2 = new Button { Text = "Start 2", Location = new Point(590, 420), Size = new Size(100, 40) };
            btnStart2.Click += BtnStart2_Click;

            btnStop2 = new Button { Text = "Stop 2", Location = new Point(730, 420), Size = new Size(100, 40) };
            btnStop2.Click += (s, e) => StopRecordingChannel2();

            

            // Add a TextBox + Button for folder path selection (Channel 2)
            var lblSavePath2 = new Label
            {
                Text = "Save Folder:",
                Location = new System.Drawing.Point(560, 100),
                AutoSize = true
            };

            var txtSavePath2 = new TextBox
            {
                Location = new System.Drawing.Point(680, 100),
                Width = 250,
                ReadOnly = true
            };

            var btnBrowseFolder2 = new Button
            {
                Text = "Browse...",
                Location = new System.Drawing.Point(940, 95),
                Size = new System.Drawing.Size(100, 30)
            };
            btnBrowseFolder2.Click += (s, e) =>
            {
                using (var folderDialog = new FolderBrowserDialog())
                {
                    folderDialog.Description = "Select a folder to save recordings";
                    folderDialog.ShowNewFolderButton = true;

                    // Try to set initial directory to E:\
                    string initialPath = @"D:\test_dump\";
                    if (Directory.Exists(initialPath))
                    {
                        folderDialog.SelectedPath = initialPath;
                    }
                    else
                    {
                        // Fallback to Desktop if D:\ is not available
                        folderDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    }

                    if (folderDialog.ShowDialog() == DialogResult.OK)
                    {
                        txtSavePath2.Text = folderDialog.SelectedPath;
                        inputPath2 = folderDialog.SelectedPath; // store for later use
                    }
                }
            };

            Controls.AddRange(new Control[] {
                lblCam1, cmbCamera1, lblMic1, cmbMic1, previewBox1, btnStart1, btnStop1,
                lblCam2, cmbCamera2, lblMic2, cmbMic2, previewBox2, btnStart2, btnStop2,
                lblSavePath1, txtSavePath1, btnBrowseFolder1,
                lblSavePath2, txtSavePath2, btnBrowseFolder2,
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

                //                using (var system = new ManagedSystem())
                var system = new ManagedSystem();
                var list = system.GetCameras();
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

                list.Clear(); // Dispose of the list after using it.

                // --- Defaults ---
                if (cmbCamera1.Items.Count > 0) cmbCamera1.SelectedIndex = 0;
                if (cmbMic1.Items.Count > 0) cmbMic1.SelectedIndex = 0;
                if (cmbCamera2.Items.Count > 1) cmbCamera2.SelectedIndex = 1;
                if (cmbMic2.Items.Count > 1) cmbMic2.SelectedIndex = 1;

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
            var camItem = cmbCamera1.SelectedItem as CameraListItem;
            if (camItem == null)
            {
                MessageBox.Show("Please select a camera for Channel 1."); return;
            }
            if (cmbMic1.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a mic for Channel 1."); return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string videoPath = Path.Combine(inputPath1, $"vid_{timestamp}.mp4");
            string audioWavPath = Path.Combine(inputPath1, $"usv_{timestamp}.wav");

            _ch1 = new Utilities.RecChannel(
                cameraIdOrIndex: camItem.Id,                // "0" or "1"
                micIndex: cmbMic1.SelectedIndex,
                videoPath: videoPath,
                audioPath: audioWavPath,
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
            var camItem = cmbCamera2.SelectedItem as CameraListItem;
            if (camItem == null)
            {
                MessageBox.Show("Please select a camera.");
                return;
            }
            if (cmbMic2.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a mic for Channel 2."); return;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string videoPath = Path.Combine(inputPath2, $"vid_{timestamp}.mp4");
            string audioWavPath = Path.Combine(inputPath2, $"usv_{timestamp}.wav");

            _ch2 = new Utilities.RecChannel(
                cameraIdOrIndex: camItem.Id,
                micIndex: cmbMic2.SelectedIndex,
                videoPath: videoPath,
                audioPath: audioWavPath,
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
        private void UpdateStartButtons()
        {
            btnStart1.Enabled = _ch1 == null;
            btnStart2.Enabled = _ch2 == null;
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