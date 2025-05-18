namespace USBWatcher
{
    partial class Main
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Main));
            splitContainer1 = new SplitContainer();
            lsvDevices = new ListView();
            columnHeader1 = new ColumnHeader();
            columnHeader2 = new ColumnHeader();
            columnHeader3 = new ColumnHeader();
            columnHeader4 = new ColumnHeader();
            columnHeader8 = new ColumnHeader();
            lsvEvents = new ListView();
            columnHeader5 = new ColumnHeader();
            columnHeader6 = new ColumnHeader();
            columnHeader7 = new ColumnHeader();
            menuStrip1 = new MenuStrip();
            settingsToolStripMenuItem = new ToolStripMenuItem();
            clearLogsToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 24);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(lsvDevices);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(lsvEvents);
            splitContainer1.Size = new Size(448, 434);
            splitContainer1.SplitterDistance = 150;
            splitContainer1.TabIndex = 0;
            // 
            // lsvDevices
            // 
            lsvDevices.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lsvDevices.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader2, columnHeader3, columnHeader4, columnHeader8 });
            lsvDevices.FullRowSelect = true;
            lsvDevices.LabelEdit = true;
            lsvDevices.Location = new Point(12, 3);
            lsvDevices.Name = "lsvDevices";
            lsvDevices.Size = new Size(424, 144);
            lsvDevices.TabIndex = 5;
            lsvDevices.UseCompatibleStateImageBehavior = false;
            lsvDevices.View = View.Details;
            // 
            // columnHeader1
            // 
            columnHeader1.Text = "Serial Device Name";
            columnHeader1.Width = 180;
            // 
            // columnHeader2
            // 
            columnHeader2.Text = "Port";
            // 
            // columnHeader3
            // 
            columnHeader3.Text = "VID";
            // 
            // columnHeader4
            // 
            columnHeader4.Text = "PID";
            // 
            // columnHeader8
            // 
            columnHeader8.Text = "SN";
            // 
            // lsvEvents
            // 
            lsvEvents.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lsvEvents.Columns.AddRange(new ColumnHeader[] { columnHeader5, columnHeader6, columnHeader7 });
            lsvEvents.FullRowSelect = true;
            lsvEvents.Location = new Point(12, 3);
            lsvEvents.Name = "lsvEvents";
            lsvEvents.Size = new Size(424, 265);
            lsvEvents.TabIndex = 4;
            lsvEvents.UseCompatibleStateImageBehavior = false;
            lsvEvents.View = View.Details;
            // 
            // columnHeader5
            // 
            columnHeader5.Text = "Time";
            columnHeader5.Width = 100;
            // 
            // columnHeader6
            // 
            columnHeader6.Text = "Device Name";
            columnHeader6.Width = 210;
            // 
            // columnHeader7
            // 
            columnHeader7.Text = "Event";
            columnHeader7.Width = 80;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { settingsToolStripMenuItem, clearLogsToolStripMenuItem, aboutToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(448, 24);
            menuStrip1.TabIndex = 1;
            menuStrip1.Text = "menuStrip1";
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            settingsToolStripMenuItem.Size = new Size(61, 20);
            settingsToolStripMenuItem.Text = "Settings";
            settingsToolStripMenuItem.Click += settingsToolStripMenuItem_Click;
            // 
            // clearLogsToolStripMenuItem
            // 
            clearLogsToolStripMenuItem.Name = "clearLogsToolStripMenuItem";
            clearLogsToolStripMenuItem.Size = new Size(71, 20);
            clearLogsToolStripMenuItem.Text = "Clear logs";
            clearLogsToolStripMenuItem.Click += clearLogsToolStripMenuItem_Click;
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(52, 20);
            aboutToolStripMenuItem.Text = "About";
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(448, 458);
            Controls.Add(splitContainer1);
            Controls.Add(menuStrip1);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "Main";
            Text = "USB Watcher";
            FormClosing += Main_FormClosing;
            Shown += Main_FormShown;
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private SplitContainer splitContainer1;
        private ListView lsvEvents;
        private ColumnHeader columnHeader5;
        private ColumnHeader columnHeader6;
        private ColumnHeader columnHeader7;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem clearLogsToolStripMenuItem;
        private ListView lsvDevices;
        private ColumnHeader columnHeader1;
        private ColumnHeader columnHeader2;
        private ColumnHeader columnHeader3;
        private ColumnHeader columnHeader4;
        private ColumnHeader columnHeader8;
        private ToolStripMenuItem settingsToolStripMenuItem;
    }
}