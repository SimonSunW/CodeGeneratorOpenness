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
using System.Globalization;
using System.Xml;

namespace CodeGeneratorOpenness
{
    public partial class frmMainForm : Form
    {
        // just a little lazzy
        public static TiaPortal tiaPortal = null;
        public static Project project = null;
        public static PlcSoftware software = null;

        private cFunctionGroups groups = new cFunctionGroups();

        public frmMainForm()
        {
            InitializeComponent();

            // avoid firewall
            // HLKM\SOFTWARE\Siemens\Automation\Openness\
            // set the rights for the key => everone to everything
            cFirewall firewall = new cFirewall();
            firewall.CalcHash();
        }

        private void frmMainForm_Load(object sender, EventArgs e)
        {
            // generate default folder
            Directory.CreateDirectory(Application.StartupPath + "\\Export");
            Directory.CreateDirectory(Application.StartupPath + "\\Import");
            Directory.CreateDirectory(Application.StartupPath + "\\Temp");
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // dispose objects
            software = null;
            project = null;

            if (tiaPortal != null)
                tiaPortal.Dispose();
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            openToolStripMenuItem_Click(this, new EventArgs());
        }

        private void IterateThroughDevices(Project project)
        {
            // no project is open
            if (project == null)
                return;

            // shop a form to indicate work
            frmReadStructure read = new frmReadStructure();
            read.Show();
            read.BringToFront();

            //Console.WriteLine(String.Format("Iterate through {0} device(s)", project.Devices.Count));
            listBox1.Items.Clear();
            listBox2.Items.Clear();

            Application.DoEvents();

            // search through devices
            foreach (Device device in project.Devices)
            {
                if (device.TypeIdentifier != null)
                {
                    // we search only for PLCs
                    if (device.TypeIdentifier == "System:Device.S71500")
                    {
                        //Console.WriteLine(String.Format("Found {0}", device.Name));
                        listBox1.Items.Add(device.Name);

                        // let's get the CPU
                        foreach (DeviceItem item in device.DeviceItems)
                        {
                            if (item.Classification.ToString() == "CPU")
                            {
                                //Console.WriteLine(String.Format("Found {0}", item.Name));
                                listBox2.Items.Add(item.Name);

                                // get the software container
                                SoftwareContainer softwareContainer = ((IEngineeringServiceProvider)item).GetService<SoftwareContainer>();
                                if (softwareContainer != null)
                                {
                                    software = softwareContainer.Software as PlcSoftware;
                                    groups.LoadTreeView(treeView1, software);
                                }
                            }
                        }
                    }
                }
            }

            // close form
            read.Close();
            read.Dispose();

            this.BringToFront();
            Application.DoEvents();

        }

        private void btnLanguage_Click(object sender, EventArgs e)
        {
            // language test for DE/EN
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
     
        private void treeView1_MouseDown(object sender, MouseEventArgs e)
        {
            // moue click on treeview

            // Make sure this is the right button.
            if (e.Button != MouseButtons.Right) return;

            // Select this node.
            TreeNode node_here = treeView1.GetNodeAt(e.X, e.Y);
            treeView1.SelectedNode = node_here;

            // See if we got a node.
            if (node_here == null) return;

            // reset menu
            ctxGroup.MenuItems[0].Enabled = true;
            ctxGroup.MenuItems[2].Enabled = true;
            //for the root node we can't delete the group
            if (node_here.Parent == null)
                ctxGroup.MenuItems[2].Enabled = false;

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
            if (node_here.Tag is PlcStruct)
            {
                ctxBlock.Show(treeView1, new Point(e.X, e.Y));
            }
        }
        
        private void mnuBlockDelete_Click(object sender, EventArgs e)
        {
            // delete block / data type
            if (treeView1.SelectedNode.Tag is PlcBlock)
            {
                PlcBlock block = (PlcBlock)treeView1.SelectedNode.Tag;

                DialogResult dlg = MessageBox.Show("Do you really want to delete the block " + block.Name + "?", "Delete block", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dlg == DialogResult.Yes)
                {
                    block.Delete();
                    IterateThroughDevices(project);
                }
            }

            if (treeView1.SelectedNode.Tag is PlcStruct)
            {
                PlcStruct block = (PlcStruct)treeView1.SelectedNode.Tag;

                DialogResult dlg = MessageBox.Show("Do you really want to delete the data type " + block.Name + "?", "Delete data type", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dlg == DialogResult.Yes)
                {
                    block.Delete();
                    IterateThroughDevices(project);
                }
            }
        }
  
        private void menuGroupDelete_Click(object sender, EventArgs e)
        {
            // delete group
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

        private void btnReload_Click(object sender, EventArgs e)
        {
            IterateThroughDevices(project);
        }

        private string getNextFileName(string fileName)
        {
            //helper to generate unique name for the export
            string extension = Path.GetExtension(fileName);

            int i = 0;
            while (File.Exists(fileName))
            {
                if (i == 0)
                    fileName = fileName.Replace(extension, "(" + ++i + ")" + extension);
                else
                    fileName = fileName.Replace("(" + i + ")" + extension, "(" + ++i + ")" + extension);
            }
            return fileName;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (project != null)
            {
                if (project.IsModified)
                    txtSaved.Text = "MODIFIED";
                else
                    txtSaved.Text = "is saved";
            }
            else
                txtSaved.Text = "no project open";
        }

        private void saveProject()
        {
            if (project != null)
            {
                if (project.IsModified)
                {
                    if (messageYesNo("Do you want to save the changes?", "Save changes") == DialogResult.Yes)
                        project.Save();
                }
            }
        }

        private void compileProject()
        {
            if (software != null)
            {
                ICompilable compileService = software.GetService<ICompilable>();
                CompilerResult result = compileService.Compile();

                this.BringToFront();
                Application.DoEvents();

                // result messages is array
                messageOK("Result : " + result.State.ToString() + "\n" +
                                "Errors: " + result.ErrorCount.ToString() + "\n" +
                                "Warnings: " + result.WarningCount.ToString() + "\n",
                                "Compiler");

                IterateThroughDevices(project);
            }
        }

        private void TiaPortal_Confirmation(object sender, ConfirmationEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private void TiaPortal_Notification(object sender, NotificationEventArgs e)
        {
            if (e.Caption == "Export Completed")
                e.IsHandled = true;

            // maybe need some more work
            if (e.Caption == "Import completed with warnings")
                e.IsHandled = true;

            //throw new NotImplementedException();
        }

        #region Dialog messageBox

        private DialogResult messageYesNo(string Message, string Title)
        {
            this.BringToFront();
            Application.DoEvents();

            return MessageBox.Show(Message, Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }

        private DialogResult messageYesNoCancel(string Message, string Title)
        {
            this.BringToFront();
            Application.DoEvents();

            return MessageBox.Show(Message, Title, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        }

        private DialogResult messageOK(string Message, string Title)
        {
            this.BringToFront();
            Application.DoEvents();

            return MessageBox.Show(Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private DialogResult messageError(string Message, string Title)
        {
            this.BringToFront();
            Application.DoEvents();

            return MessageBox.Show(Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        #endregion

        #region menu items

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // if no project is open
            if (project == null)
            {
                // see if we have a open instance and use the first one
                foreach (TiaPortalProcess tiaPortalProcess in TiaPortal.GetProcesses())
                {
                    TiaPortalProcess p = TiaPortal.GetProcess(tiaPortalProcess.Id);

                    tiaPortal = p.Attach();
                    break;
                }

                // we don't habe an instance then open a new instance
                if (tiaPortal == null)
                {
                    tiaPortal = new TiaPortal(TiaPortalMode.WithUserInterface);
                }

                tiaPortal.Notification += TiaPortal_Notification;
                tiaPortal.Confirmation += TiaPortal_Confirmation;

                // let's get the projects
                ProjectComposition projects = tiaPortal.Projects;

                // no open project - then open file dialog
                if (projects.Count == 0)
                {
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
                            return; // no file selected
                    }
                }
                else
                {
                    // for now we use the first project
                    project = tiaPortal.Projects[0];
                }

                txtProject.Text = "Project: " + project.Name;

                // loop through the data
                IterateThroughDevices(project);
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveProject();
        }

        private void compileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            compileProject();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (project != null)
            {
                saveProject();
            }
            Application.Exit();
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (project != null)
            {
                saveProject();

                listBox1.Items.Clear();
                listBox2.Items.Clear();

                treeView1.Nodes.Clear();

                txtProject.Text = "Project: ";
                txtSaved.Text = "...";

                project.Close();

                software = null;
                project = null;
            }
        }

        private void blocksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (software == null)
                return;

            DialogResult res;

            if (treeView1.SelectedNode != null)
            {
                var sel = treeView1.SelectedNode.Tag;
                if (sel is PlcBlockGroup)
                {
                    try
                    {
                        string importPath = (string)Properties.Settings.Default["PathImportBlock"];
                        if (importPath == string.Empty) importPath = Application.StartupPath + "\\Import";

                        using (OpenFileDialog openFileDialog = new OpenFileDialog())
                        {
                            openFileDialog.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
                            openFileDialog.FilterIndex = 1;
                            openFileDialog.InitialDirectory = importPath;

                            if (openFileDialog.ShowDialog() == DialogResult.OK)
                            {
                                Properties.Settings.Default["PathImportBlock"] = openFileDialog.FileName;
                                Properties.Settings.Default.Save();

                                PlcBlockGroup group = (PlcBlockGroup)sel;
                                string fPath = openFileDialog.FileName;
                                FileInfo f = new FileInfo(fPath);

                                // now load the xml document
                                XmlDocument xmlDoc = new XmlDocument();
                                xmlDoc.Load(fPath);

                                // get version of the file
                                XmlNode bkm = xmlDoc.SelectSingleNode("//Document//Engineering");
                                string version = bkm.Attributes["version"].Value;

                                string blockType = string.Empty;

                                XmlNode document = xmlDoc.SelectSingleNode("//Document");
                                foreach (XmlNode node in document.ChildNodes)
                                {
                                    if (node.Name.StartsWith("SW.Blocks."))
                                    {
                                        blockType = node.Name.Substring(10);
                                        Console.WriteLine(node.Name.Substring(10));
                                    }
                                }

                                // we found the type of the software
                                if (blockType != string.Empty)
                                {
                                    // get the name of the data type
                                    XmlNode nameDefination = xmlDoc.SelectSingleNode("//Document//SW.Blocks." + blockType + "//AttributeList//Name");
                                    string name = nameDefination.InnerText;

                                    // check if the data type exists
                                    if (!groups.NameExists(name, software))
                                    {
                                        // import the file
                                        group.Blocks.Import(f, ImportOptions.None);
                                        IterateThroughDevices(project);
                                    }
                                    else
                                    {
                                        // overwrite? yes = overwrite / no = new name / cancel = just cancel
                                        res = MessageBox.Show("Data block " + name + " exists already. Overwrite(Yes) or Rename(No) ?",
                                                              "Overwrite / Rename",
                                                              MessageBoxButtons.YesNoCancel,
                                                              MessageBoxIcon.Question);

                                        if (res == DialogResult.Yes)
                                        {
                                            // overwrite plc block
                                            group.Blocks.Import(f, ImportOptions.Override);
                                            IterateThroughDevices(project);
                                        }
                                        else if (res == DialogResult.No)
                                        {
                                            // with a different name we need to save a copy 

                                            res = DialogResult.OK;
                                            while (groups.NameExists(name, software) && res == DialogResult.OK)
                                            {
                                                res = Input.InputBox("New block name", "Enter a new block name", ref name);
                                            }
                                            // we don't cancel, so import with new name
                                            if (res == DialogResult.OK)
                                            {
                                                nameDefination.InnerText = name;

                                                xmlDoc.Save(Application.StartupPath + "\\Temp\\temp.xml");
                                                f = new FileInfo(Application.StartupPath + "\\Temp\\temp.xml");

                                                group.Blocks.Import(f, ImportOptions.None);
                                                IterateThroughDevices(project);
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    messageOK("The file " + Path.GetFileName(openFileDialog.FileName) + " is not PLC block",
                                              "Not a PLC block file");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        messageError(ex.Message,
                                     "Exception");
                    }
                }
                else
                {
                    messageOK("Not a group selected!",
                              "Not a group");
                }
            }
            else
            {
                messageOK("Nothing selected!",
                          "Select a group");
            }
        }

        private void exportToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            // TODO : culture
            if (project != null)
            {
                try
                {
                    string filePath = (string)Properties.Settings.Default["PathLanguageText"];
                    if (filePath == string.Empty) filePath = Application.StartupPath + "\\Export";

                    using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                    {
                        saveFileDialog.Filter = "XML files (*.xlsx)|*.xlsx";
                        saveFileDialog.FilterIndex = 1;
                        saveFileDialog.InitialDirectory = filePath;
                        saveFileDialog.FileName = "TIAProjectTexts.xlsx";
                        saveFileDialog.OverwritePrompt = false;

                        if (saveFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            Properties.Settings.Default["PathImportBlock"] = saveFileDialog.FileName;
                            Properties.Settings.Default.Save();

                            // the API can not overwrite
                            filePath = getNextFileName(saveFileDialog.FileName);
                            project.ExportProjectTexts(new FileInfo(filePath), new CultureInfo("de-DE"), new CultureInfo("en-GB"));

                            messageOK("File " + Path.GetFileName(filePath) + " has been exported",
                                      "Export language file");
                        }
                    }
                }
                catch (Exception ex)
                {
                    messageError(ex.Message,
                                 "Exception");
                }
            }
        }

        private void importToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            // TODO : culture
            if (project != null)
            {
                try
                {
                    string filePath = (string)Properties.Settings.Default["PathLanguageText"];
                    if (filePath == string.Empty) filePath = Application.StartupPath + "\\Export";

                    using (OpenFileDialog openFileDialog = new OpenFileDialog())
                    {
                        openFileDialog.Filter = "XML files (*.xlsx)|*.xlsx";
                        openFileDialog.FilterIndex = 1;
                        openFileDialog.InitialDirectory = filePath;
                        openFileDialog.FileName = "TIAProjectTexts.xlsx";

                        if (openFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            project.ImportProjectTexts(new FileInfo(openFileDialog.FileName), true);

                            messageOK("File " + Path.GetFileName(openFileDialog.FileName) + " has been imported",
                                      "Import language file");
                        }
                    }
                }
                catch (Exception ex)
                {
                    messageError(ex.Message,
                                 "Exception");
                }
            }
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // nothing selected            
            if (treeView1.SelectedNode == null) return;
            // only blocks and structs
            if (!(treeView1.SelectedNode.Tag is PlcBlock) && !(treeView1.SelectedNode.Tag is PlcStruct))
                return;

            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            folderDialog.Description = "Select export path";
            folderDialog.SelectedPath = Application.StartupPath + "\\Export";

            DialogResult res = folderDialog.ShowDialog();
            // ok now save
            if (res == DialogResult.OK)
            {
                try
                {
                    // for plcBlocks like OB,FB,FC
                    if (treeView1.SelectedNode.Tag is PlcBlock)
                    {
                        PlcBlock block = (PlcBlock)treeView1.SelectedNode.Tag;

                        string fPath = Application.StartupPath + "\\Export\\plcBlock_" + block.ProgrammingLanguage.ToString() + "_" + block.Name + ".xml";
                        fPath = getNextFileName(fPath);

                        FileInfo f = new FileInfo(fPath);
                        block.Export(f, ExportOptions.None);

                        messageOK("File " + f + " has beed exported",
                                  "Export");
                    }

                    // for data types
                    if (treeView1.SelectedNode.Tag is PlcStruct)
                    {
                        PlcStruct block = (PlcStruct)treeView1.SelectedNode.Tag;

                        string fPath = Application.StartupPath + "\\Export\\plcBlock_" + block.Name + ".xml";
                        fPath = getNextFileName(fPath);

                        FileInfo f = new FileInfo(fPath);
                        block.Export(f, ExportOptions.None);

                        messageOK("File " + f + " has beed exported",
                                  "Export");
                    }
                }
                catch (Exception ex)
                {
                    messageError(ex.Message,
                                 "Exception");
                }
            }
        }

        private void dataTypesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (software == null)
                return;

            try
            {
                // test file
                string fPath = string.Empty;
                //string fPath = Application.StartupPath + "\\Step.xml";

                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "XML files (*.xnl)|*.xml|All files (*.*)|*.*";
                    openFileDialog.FilterIndex = 1;
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        //Get the path of specified file
                        fPath = openFileDialog.FileName;
                        FileInfo f = new FileInfo(fPath);

                        // now load the xml document
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.Load(fPath);

                        // get version of the file
                        XmlNode bkm = xmlDoc.SelectSingleNode("//Document//Engineering");
                        string version = bkm.Attributes["version"].Value;

                        // check the correct type
                        XmlNode dataType = xmlDoc.SelectSingleNode("//Document//SW.Types.PlcStruct");
                        if (dataType != null)
                        {
                            // get the name of the data type
                            XmlNode nameDefination = xmlDoc.SelectSingleNode("//Document//SW.Types.PlcStruct//AttributeList//Name");
                            string name = nameDefination.InnerText;

                            // check if the name exists
                            bool exists = false;

                            List<string> list = new List<string>();
                            list = groups.GetAllBlocksNames(software.BlockGroup, list);
                            if (list.Contains(name)) exists = true;

                            if (!exists)
                            {
                                list = new List<string>();
                                list = groups.GetAllDataTypesNames(software.TypeGroup, list);
                                if (list.Contains(name)) exists = true;

                                if (!exists)
                                {
                                    // import the file
                                    software.TypeGroup.Types.Import(f, ImportOptions.None);
                                    IterateThroughDevices(project);
                                }
                                else
                                {
                                    // overwrite?
                                    DialogResult res = MessageBox.Show("Data type " + name + " exists already. Overwrite ?",
                                                                       "Overwrite",
                                                                       MessageBoxButtons.OKCancel,
                                                                       MessageBoxIcon.Question);
                                    if (res == DialogResult.OK)
                                    {
                                        // overwrite data type
                                        software.TypeGroup.Types.Import(f, ImportOptions.Override);
                                        IterateThroughDevices(project);
                                    }
                                }
                            }
                            else
                                // plc block exist
                                messageError("PLC block with the name " + name + " exist!",
                                             "Name exits");
                        }
                        else
                        {
                            // wrong data type
                            messageError("Wrong XML file (PlcStruct) ?",
                                         "Wrong file");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                messageError(ex.Message,
                             "Exception");
            }
        }

        #endregion
    }
}
