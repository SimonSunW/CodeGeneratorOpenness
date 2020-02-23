﻿///
/// Sample applicatin for automated code generation for Siemens TIA Portal with Openness Interface
/// 
/// by Mark König @ 02/2020
/// 
/// build to 64 bit, since we nedd to access the registry for the TIA firewall
///

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.Library;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Win32;
using System.Globalization;

namespace CodeGeneratorOpenness
{
    public partial class frmMainForm : Form
    {
        // just a little lazzy
        public static TiaPortal tiaPortal = null;
        public static Project project = null;
        public static PlcSoftware software = null;

        public frmMainForm()
        {
            InitializeComponent();

            // avoid firewall
            CalcHash();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // dispose objects

            project = null;

            if (tiaPortal != null)
                tiaPortal.Dispose();
        }

        // open project
        private void button1_Click(object sender, EventArgs e)
        {
            if (project == null)
            {
                // see if we have a open instance and use the first one
                foreach (TiaPortalProcess tiaPortalProcess in TiaPortal.GetProcesses())
                {
                    TiaPortalProcess p = TiaPortal.GetProcess(tiaPortalProcess.Id);

                    tiaPortal = p.Attach();
                    Console.WriteLine("TIA Portal has been attached");
                    break;
                }

                // we don't habe an instance then open a new instance
                if (tiaPortal == null)
                {
                    tiaPortal = new TiaPortal(TiaPortalMode.WithUserInterface);
                    Console.WriteLine("TIA Portal has started");
                }

                // let's get the projects
                ProjectComposition projects = tiaPortal.Projects;

                // projects available ?
                Console.WriteLine("TIA projects " + projects.Count.ToString());

                // no open project - then open file dialog
                if (projects.Count == 0)
                {
                    Console.WriteLine("Opening Project...");

                    string filePath = string.Empty;
                    using (OpenFileDialog openFileDialog = new OpenFileDialog())
                    {
                        openFileDialog.Filter = "V16 project files (*.ap16)|*.ap16|All files (*.*)|*.*";
                        openFileDialog.FilterIndex = 1;
                        openFileDialog.RestoreDirectory = true;

                        if (openFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            //Get the path of specified file
                            filePath = openFileDialog.FileName;

                            // load projectpath
                            FileInfo projectPath = new FileInfo(filePath);
                            try
                            {
                                project = projects.Open(projectPath);
                            }
                            catch (Exception)
                            {
                                Console.WriteLine(String.Format("Could not open project {0}", projectPath.FullName));
                                Application.Exit();
                            }
                        }
                        else
                            Application.Exit(); // no file selected
                    }
                }
                else
                {
                    // for now we use the first project
                    project = tiaPortal.Projects[0];
                }

                Console.WriteLine(String.Format("Project {0} is open", project.Path.FullName));

                // loop through the data
                IterateThroughDevices(project);
            }
        }

        private void IterateThroughDevices(Project project)
        {
            // no project is open
            if (project == null)
                return;

            // shop a form to indicate work
            frmReadStructure read = new frmReadStructure();
            read.Show();
            Application.DoEvents();

            Console.WriteLine(String.Format("Iterate through {0} device(s)", project.Devices.Count));
            listBox1.Items.Clear();
            listBox2.Items.Clear();
            treeView1.Nodes.Clear();

            // search through devices
            foreach (Device device in project.Devices)
            {
                if (device.TypeIdentifier != null)
                {
                    // we search only for PLCs
                    if (device.TypeIdentifier == "System:Device.S71500")
                    {
                        Console.WriteLine(String.Format("Found {0}", device.Name));
                        listBox1.Items.Add(device.Name);

                        // let's get the CPU
                        foreach (DeviceItem item in device.DeviceItems)
                        {
                            if (item.Classification.ToString() == "CPU")
                            {
                                Console.WriteLine(String.Format("Found {0}", item.Name));
                                listBox2.Items.Add(item.Name);

                                // get the software container
                                SoftwareContainer softwareContainer = ((IEngineeringServiceProvider)item).GetService<SoftwareContainer>();
                                if (softwareContainer != null)
                                {
                                    software = softwareContainer.Software as PlcSoftware;
                                    Console.WriteLine("Found : " + software.Name);

                                    // start update treeview
                                    treeView1.BeginUpdate();

                                    // add root node
                                    TreeNode root = new TreeNode(software.Name);
                                    root.Tag = software;
                                    treeView1.Nodes.Add(root);

                                    AddPlcBlocks(software.BlockGroup, root);

                                    // end update
                                    treeView1.EndUpdate();

                                    root.Expand();
                                }
                            }
                        }
                    }
                }
            }

            // close form
            read.Close();
            read.Dispose();

        }

        // add structure to treeview
        private void AddPlcBlocks(PlcBlockGroup plcGroup, TreeNode node)
        {
            // first add all plc blocks
            foreach (PlcBlock plcBlock in plcGroup.Blocks)
            {
                Console.WriteLine("Found block : " + plcBlock.Name);

                TreeNode n = new TreeNode(plcBlock.Name);
                n.Tag = plcBlock;

                node.Nodes.Add(n);
            }
            // then add groups and search recursive
            foreach (PlcBlockGroup group in plcGroup.Groups)
            {
                Console.WriteLine("Found group : " + group.Name);

                TreeNode n = new TreeNode(group.Name);
                n.Tag = group;

                AddPlcBlocks(group, n);
                node.Nodes.Add(n);
            }
        }

        // close project
        private void button2_Click(object sender, EventArgs e)
        {
            if (project != null)
            {
                listBox1.Items.Clear();
                listBox2.Items.Clear();
                treeView1.Nodes.Clear();

                project.Close();
                project = null;
            }
        }

        // language test for DE/EN
        private void button3_Click(object sender, EventArgs e)
        {
            if (project != null)
            {
                LanguageSettings languageSettings = project.LanguageSettings;
                LanguageComposition supportedLanguages = languageSettings.Languages;
                LanguageAssociation activeLanguages = languageSettings.ActiveLanguages;

                Language supportedGermanLanguage = supportedLanguages.Find(CultureInfo.GetCultureInfo("de-DE"));
                Language supportedEnglishLanguage = supportedLanguages.Find(CultureInfo.GetCultureInfo("en-GB"));

                // add german if needed
                Language l = activeLanguages.Find(CultureInfo.GetCultureInfo("de-DE"));
                if (l == null)
                    activeLanguages.Add(supportedGermanLanguage);
                // add english if needed
                l = activeLanguages.Find(CultureInfo.GetCultureInfo("en-GB"));
                if (l == null)
                    activeLanguages.Add(supportedEnglishLanguage);

                // set edit languages
                languageSettings.EditingLanguage = supportedGermanLanguage;
                languageSettings.ReferenceLanguage = supportedGermanLanguage;


            }
        }

        // calculation for firewall
        public void CalcHash()
        {
            // calc the hash for the file for the firwall settings
            string applicationPath = Application.StartupPath + "\\CodeGeneratorOpenness.exe";
            string lastWriteTimeUtcFormatted = String.Empty;
            DateTime lastWriteTimeUtc;
            HashAlgorithm hashAlgorithm = SHA256.Create();
            FileStream stream = File.OpenRead(applicationPath);
            byte[] hash = hashAlgorithm.ComputeHash(stream);
            // this is how the hash should appear in the .reg file
            string convertedHash = Convert.ToBase64String(hash);
            FileInfo fileInfo = new FileInfo(applicationPath);
            lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
            // this is how the last write time should be formatted
            lastWriteTimeUtcFormatted = lastWriteTimeUtc.ToString(@"yyyy\/MM\/dd HH:mm:ss.fff");

            Console.WriteLine("CRC _: " + convertedHash);
            Console.WriteLine("Date : " + lastWriteTimeUtcFormatted);

            // we set the key in the registry to avoid the firewall each time
            try
            {
                // first time we need to ack, then the key is present
                RegistryKey rk = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Siemens\Automation\Openness\16.0\Whitelist\CodeGeneratorOpenness.exe\Entry", true);
                rk.SetValue("FileHash", convertedHash);
                rk.SetValue("DateModified", lastWriteTimeUtcFormatted);
            }
            catch
            { }
        }

        // add a fc for testing
        private void button4_Click(object sender, EventArgs e)
        {
            if (software == null)
            {
                Console.WriteLine("Software cannot be null");
                return;
            }

            string fPath = Application.StartupPath + "\\TestFC1.xml";

            FileInfo f = new FileInfo(fPath);
            software.BlockGroup.Blocks.Import(f, ImportOptions.Override);

            IterateThroughDevices(project);

        }

        // moue click on treeview
        private void treeView1_MouseDown(object sender, MouseEventArgs e)
        {
            // Make sure this is the right button.
            if (e.Button != MouseButtons.Right) return;

            // Select this node.
            TreeNode node_here = treeView1.GetNodeAt(e.X, e.Y);
            treeView1.SelectedNode = node_here;

            // See if we got a node.
            if (node_here == null) return;

            // See what kind of object this is and
            // display the appropriate popup menu.
            if (node_here.Tag is PlcSoftware)
            {
                ctxSoftware.Show(treeView1, new Point(e.X, e.Y));
            }
            if (node_here.Tag is PlcBlockGroup)
            {
                ctxGroup.Show(treeView1, new Point(e.X, e.Y));
            }
            if (node_here.Tag is PlcBlock)
            {
                ctxBlock.Show(treeView1, new Point(e.X, e.Y));
            }
        }

        // delete block
        private void mnuBlockDelete_Click(object sender, EventArgs e)
        {
            PlcBlock block = (PlcBlock)treeView1.SelectedNode.Tag;

            DialogResult dlg = MessageBox.Show("Do you really want to delete the block " + block.Name + "?", "Delete block", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dlg == DialogResult.Yes)
            {
                block.Delete();
                IterateThroughDevices(project);
            }
        }

        // delete group
        private void menuGroupDelete_Click(object sender, EventArgs e)
        {
            PlcBlockGroup group = (PlcBlockGroup)treeView1.SelectedNode.Tag;

            if (group.Blocks.Count > 0)
            {
                MessageBox.Show("The group " + group.Name + " has ^sub blocks!\nDelete this blocks first", "Delete group", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (group.Groups.Count > 0)
            {
                MessageBox.Show("The group " + group.Name + " has sub groups!\nDelete this groups first", "Delete group", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            DialogResult dlg = MessageBox.Show("Do you really want to delete the group " + group.Name + "?", "Delete block", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dlg == DialogResult.Yes)
            {
                // not implemented yet, there is no method to delete a group?    

                // code from Tia Example is using invoke
                var selectedProjectObject = treeView1.SelectedNode.Tag;
                try
                {
                    var engineeringObject = selectedProjectObject as IEngineeringObject;
                    engineeringObject?.Invoke("Delete", new Dictionary<Type, object>());
                }
                catch (EngineeringException)
                {

                }
                IterateThroughDevices(project);
            }
        }

        private void menuGroupAdd_Click(object sender, EventArgs e)
        {
            PlcBlockGroup group = (PlcBlockGroup)treeView1.SelectedNode.Tag;

            string name = string.Empty;
            DialogResult dlg = Input.InputBox("Enter new group name", "New group", ref name);

            if (dlg == DialogResult.OK)
            {
                if (name != string.Empty)
                {
                    group.Groups.Create(name);
                    IterateThroughDevices(project);
                }
            }
        }

        private void menuSofwareAdd_Click(object sender, EventArgs e)
        {
            PlcSoftware soft = (PlcSoftware)treeView1.SelectedNode.Tag;
            PlcBlockGroup group = soft.BlockGroup;

            string name = string.Empty;
            DialogResult dlg = Input.InputBox("Enter new group name", "New group", ref name);

            if (dlg == DialogResult.OK)
            {
                if (name != string.Empty)
                {
                    group.Groups.Create(name);
                    IterateThroughDevices(project);
                }
            }
        }
    }
}