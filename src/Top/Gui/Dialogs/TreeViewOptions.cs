
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Xml;

using NetFocus.Components.AddIns;
using NetFocus.DataStructure.Services;
using NetFocus.DataStructure.Properties;
using NetFocus.Components.AddIns.Codons;
using NetFocus.DataStructure.Gui.XmlForms;
using NetFocus.DataStructure.Gui.Dialogs.OptionPanels;

namespace NetFocus.DataStructure.Gui.Dialogs
{
	public class GradientHeaderPanel : Label
	{
		public GradientHeaderPanel(int fontSize) : this()
		{
			Font = new Font("Tahoma", fontSize);
		}
		
		public GradientHeaderPanel() : base()
		{
			ResourceService ResourceService = (ResourceService)ServiceManager.Services.GetService(typeof(ResourceService));
			
			ResizeRedraw = true;
			Text = String.Empty;
		}

		protected override void OnPaintBackground(PaintEventArgs pe)
		{
			base.OnPaintBackground(pe);
			Graphics g = pe.Graphics;
			
			using (Brush brush = new LinearGradientBrush(new Point(0, 0), new Point(Width, Height),
			                                             SystemColors.Window, SystemColors.Control)) {
				g.FillRectangle(brush, new Rectangle(0, 0, Width, Height));
			}
		}
	}
	
	/// <summary>
	/// 一个选项对话框，用来设置系统的一些属性。
	/// </summary>
	public class TreeViewOptions : BaseXmlForm
	{
		GradientHeaderPanel optionsPanelLabel;
		bool b = true;
		protected ArrayList OptionPanels          = new ArrayList();
		
		IProperties properties = null;
		
		Font plainFont = null;
		Font boldFont  = null;
		
		public IProperties Properties {
			get {
				return properties;
			}
		}
		
		
		void AcceptEvent(object sender, EventArgs e)
		{
			foreach (AbstractOptionPanel panel in OptionPanels) {
				if (!panel.ReceiveDialogMessage(DialogMessage.OK)) {
					return;
				}
			}
			DialogResult = DialogResult.OK;
		}
		
		void BeforeExpandNode(object sender, TreeViewCancelEventArgs e)
		{
			if (!b) {
				return;
			}
			b = false;
			((TreeView)ControlDictionary["optionsTreeView"]).BeginUpdate();
			// search first leaf node (leaf nodes have no children)
			TreeNode node = e.Node.FirstNode;
			while (node.Nodes.Count > 0) {
				node = node.FirstNode;
			}
			((TreeView)ControlDictionary["optionsTreeView"]).CollapseAll();
			node.EnsureVisible();
			node.ImageIndex = 3;
			((TreeView)ControlDictionary["optionsTreeView"]).EndUpdate();
			SetOptionPanelTo(node);
			b = true;
		}
		
		void BeforeSelectNode(object sender, TreeViewCancelEventArgs e)
		{
			ResetImageIndex(((TreeView)ControlDictionary["optionsTreeView"]).Nodes);
			if (b) {
				CollapseOrExpandNode(e.Node);
			}
		}
		
		void HandleClick(object sender, EventArgs e)
		{
			if (((TreeView)ControlDictionary["optionsTreeView"]).GetNodeAt(((TreeView)ControlDictionary["optionsTreeView"]).PointToClient(Control.MousePosition)) == ((TreeView)ControlDictionary["optionsTreeView"]).SelectedNode && b) {
				CollapseOrExpandNode(((TreeView)ControlDictionary["optionsTreeView"]).SelectedNode);
			}
		}
		
		void CollapseOrExpandNode(TreeNode node)
		{
			if (node.Nodes.Count > 0) {  // only folders
				if (node.IsExpanded) {
					node.Collapse();
				}  else {
					node.Expand();			
				}
			}
		}
		
		void ResetImageIndex(TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes) 
			{
				if (node.Nodes.Count > 0) 
				{
					ResetImageIndex(node.Nodes);
				} 
				else 
				{
					node.ImageIndex         = 2;
					node.SelectedImageIndex = 3;
				}
			}
		}
		
		void SetOptionPanelTo(TreeNode node)
		{
			IDialogPanelDescriptor descriptor = node.Tag as IDialogPanelDescriptor;
			if (descriptor != null && descriptor.DialogPanel != null && descriptor.DialogPanel.Control != null) {
				descriptor.DialogPanel.ReceiveDialogMessage(DialogMessage.Activated);
				ControlDictionary["optionControlPanel"].Controls.Clear();
				ControlDictionary["optionControlPanel"].Controls.Add(descriptor.DialogPanel.Control);
				optionsPanelLabel.Text = descriptor.Label;
			}
		}
		
		void TreeMouseDown(object sender, MouseEventArgs e)
		{
			TreeNode node = ((TreeView)ControlDictionary["optionsTreeView"]).GetNodeAt(((TreeView)ControlDictionary["optionsTreeView"]).PointToClient(Control.MousePosition));
			if (node != null) {
				if (node.Nodes.Count == 0) ((TreeView)ControlDictionary["optionsTreeView"]).SelectedNode = node;
			}
		}
		
		void AddNodes(object customizer, TreeNodeCollection nodes, ArrayList dialogPanelDescriptors)
		{
			nodes.Clear();
			foreach (IDialogPanelDescriptor descriptor in dialogPanelDescriptors) {
				if (descriptor.DialogPanel != null) { // may be null, if it is only a "path"
					descriptor.DialogPanel.CustomizationObject = customizer;
					descriptor.DialogPanel.Control.Dock = DockStyle.Fill;
					OptionPanels.Add(descriptor.DialogPanel);
				}
				
				TreeNode newNode = new TreeNode(descriptor.Label);
				newNode.Tag = descriptor;
				newNode.NodeFont = plainFont;
				nodes.Add(newNode);
				if (descriptor.DialogPanelDescriptors != null) {
					AddNodes(customizer, newNode.Nodes, descriptor.DialogPanelDescriptors);
				}
			}
		}
		
		void SelectNode(object sender, TreeViewEventArgs e)
		{
			SetOptionPanelTo(((TreeView)ControlDictionary["optionsTreeView"]).SelectedNode);
		}
		
		void InitImageList()
		{
			ImageList imglist = new ImageList();
			imglist.ColorDepth = ColorDepth.Depth32Bit;
			imglist.Images.Add(ResourceService.GetBitmap("Icons.16x16.ClosedFolderBitmap"));
			imglist.Images.Add(ResourceService.GetBitmap("Icons.16x16.OpenFolderBitmap"));
			imglist.Images.Add(new Bitmap(1, 1));
			//imglist.Images.Add(ResourceService.GetBitmap("Icons.16x16.SelectionArrow"));
			imglist.Images.Add(ResourceService.GetBitmap("Icons.AboutImage"));
			
			((TreeView)ControlDictionary["optionsTreeView"]).ImageList = imglist;
		}
		
		void ShowOpenFolderIcon(object sender, TreeViewCancelEventArgs e)
		{
			if (e.Node.Nodes.Count > 0) {
				e.Node.ImageIndex = e.Node.SelectedImageIndex = 1;
			}
		}
		
		void ShowClosedFolderIcon(object sender, TreeViewCancelEventArgs e)
		{
			if (e.Node.Nodes.Count > 0) {
				e.Node.ImageIndex = e.Node.SelectedImageIndex = 0;
			}
		}
		
		
		public TreeViewOptions(IProperties properties, IAddInTreeNode node) 
		{
			this.properties = properties;
			
			this.Text = StringParserService.Parse("${res:Dialog.Options.TreeViewOptions.DialogName}");

			this.InitializeComponent();
			
			plainFont = new Font(((TreeView)ControlDictionary["optionsTreeView"]).Font, FontStyle.Regular);
			boldFont  = new Font(((TreeView)ControlDictionary["optionsTreeView"]).Font, FontStyle.Bold);
			
			InitImageList();
			
			if (node != null) {
				AddNodes(properties, ((TreeView)ControlDictionary["optionsTreeView"]).Nodes, node.BuildChildItems(this));
			}
		}
		
		
		void InitializeComponent() 
		{
			base.SetupFromXmlFile(Path.Combine(PropertyService.DataDirectory, @"resources\panels\TreeViewOptionsDialog.xfrm"));
			this.optionsPanelLabel = new GradientHeaderPanel();
			this.optionsPanelLabel.Font        = new System.Drawing.Font("Tahoma", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.optionsPanelLabel.TextAlign   = System.Drawing.ContentAlignment.MiddleLeft;
			this.optionsPanelLabel.BorderStyle = BorderStyle.Fixed3D;
			this.optionsPanelLabel.Dock        = DockStyle.Fill;
			ControlDictionary["headerPanel"].Controls.Add(optionsPanelLabel);
			Owner = (Form)WorkbenchSingleton.Workbench;
			Icon  = null;
			ControlDictionary["okButton"].Click += new EventHandler(AcceptEvent);
			
			((TreeView)ControlDictionary["optionsTreeView"]).Click          += new EventHandler(HandleClick);
			((TreeView)ControlDictionary["optionsTreeView"]).AfterSelect    += new TreeViewEventHandler(SelectNode);
			((TreeView)ControlDictionary["optionsTreeView"]).BeforeSelect   += new TreeViewCancelEventHandler(BeforeSelectNode);
			((TreeView)ControlDictionary["optionsTreeView"]).BeforeExpand   += new TreeViewCancelEventHandler(BeforeExpandNode);
			((TreeView)ControlDictionary["optionsTreeView"]).BeforeExpand   += new TreeViewCancelEventHandler(ShowOpenFolderIcon);
			((TreeView)ControlDictionary["optionsTreeView"]).BeforeCollapse += new TreeViewCancelEventHandler(ShowClosedFolderIcon);
			((TreeView)ControlDictionary["optionsTreeView"]).MouseDown      += new MouseEventHandler(TreeMouseDown);
		}
	}
}
