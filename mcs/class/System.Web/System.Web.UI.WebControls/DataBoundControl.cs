//
// System.Web.UI.WebControls.DataBoundControl
//
// Authors:
//	Ben Maurer (bmaurer@users.sourceforge.net)
//	Sanjay Gupta (gsanjay@novell.com)
//
// (C) 2003 Ben Maurer
// (C) 2004 Novell, Inc. (http://www.novell.com)
//

//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

#if NET_2_0
using System.Collections;
using System.Collections.Specialized;
using System.Text;
using System.Web.Util;
using System.ComponentModel;
using System.Security.Permissions;

namespace System.Web.UI.WebControls {

	// CAS
	[AspNetHostingPermission (SecurityAction.LinkDemand, Level = AspNetHostingPermissionLevel.Minimal)]
	[AspNetHostingPermission (SecurityAction.InheritanceDemand, Level = AspNetHostingPermissionLevel.Minimal)]
	// attributes
	[DesignerAttribute ("System.Web.UI.Design.WebControls.DataBoundControlDesigner, " + Consts.AssemblySystem_Design, "System.ComponentModel.Design.IDesigner")]
	public abstract class DataBoundControl : BaseDataBoundControl
	{
		DataSourceSelectArguments selectArguments;
		DataSourceView currentView;

		protected DataBoundControl ()
		{
		}

		/* Used for controls that used to inherit from
		 * WebControl, so the tag can propagate upwards
		 */
		internal DataBoundControl (HtmlTextWriterTag tag) : base (tag)
		{
		}
		
		
		protected virtual IDataSource GetDataSource ()
		{
			if (IsBoundUsingDataSourceID) {

				Control ctrl = FindDataSource ();

				if (ctrl == null)
					throw new HttpException (string.Format ("A control with ID '{0}' could not be found.", DataSourceID));
				if (!(ctrl is IDataSource))
					throw new HttpException (string.Format ("The control with ID '{0}' is not a control of type IDataSource.", DataSourceID));
				return (IDataSource) ctrl;
			}
			
			IDataSource ds = DataSource as IDataSource;
			if (ds != null) return ds;
			
			IEnumerable ie = DataSourceResolver.ResolveDataSource (DataSource, DataMember);
			return new CollectionDataSource (ie);
		}
		
		protected virtual DataSourceView GetData ()
		{
			if (currentView == null)
				UpdateViewData ();
			return currentView;
		}
		
		DataSourceView InternalGetData ()
		{
			if (DataSource != null && IsBoundUsingDataSourceID)
				throw new HttpException ("Control bound using both DataSourceID and DataSource properties.");
			
			IDataSource ds = GetDataSource ();
			if (ds != null)
				return ds.GetView (DataMember);
			else
				return null; 
		}
		
		protected override void OnDataPropertyChanged ()
		{
			base.OnDataPropertyChanged ();
			currentView = null;
		}
		
		protected virtual void OnDataSourceViewChanged (object sender, EventArgs e)
		{
			RequiresDataBinding = true;
		}
		
		// MSDN: The OnPagePreLoad method is overridden by the DataBoundControl class 
		// to set the BaseDataBoundControl.RequiresDataBinding property to true in 
		// cases where the HTTP request is a postback and view state is enabled but 
		// the data-bound control has not yet been bound. 
		protected override void OnPagePreLoad (object sender, EventArgs e)
		{
			base.OnPagePreLoad (sender, e);

			Initialize ();
		}

		private void Initialize ()
		{
			if (!IsDataBound)
				RequiresDataBinding = true;

			UpdateViewData ();
		}
		
		void UpdateViewData ()
		{
			DataSourceView view = InternalGetData ();
			if (view == currentView) return;

			if (currentView != null)
				currentView.DataSourceViewChanged -= new EventHandler (OnDataSourceViewChanged);

			currentView = view;

			if (view != null)
				view.DataSourceViewChanged += new EventHandler (OnDataSourceViewChanged);
		}
		
		protected internal override void OnLoad (EventArgs e)
		{
			if (!Initialized) {
				
				Initialize ();

				// MSDN: The ConfirmInitState method sets the initialized state of the data-bound 
				// control. The method is called by the DataBoundControl class in its OnLoad method.
				ConfirmInitState ();
			}
			base.OnLoad(e);
		}
		
		protected internal virtual void PerformDataBinding (IEnumerable data)
		{
		}

		protected override void ValidateDataSource (object dataSource)
		{
			if (dataSource == null || dataSource is IListSource || dataSource is IEnumerable || dataSource is IDataSource)
				return;
			throw new ArgumentException ("Invalid data source source type. The data source must be of type IListSource, IEnumerable or IDataSource.");
		}

		[ThemeableAttribute (false)]
		[DefaultValueAttribute ("")]
		[WebCategoryAttribute ("Data")]
		public virtual string DataMember {
			get { return ViewState.GetString ("DataMember", ""); }
			set { ViewState["DataMember"] = value; }
		}

		[IDReferencePropertyAttribute (typeof(DataSourceControl))]
		public override string DataSourceID {
			get { return ViewState.GetString ("DataSourceID", ""); }
			set {
				ViewState ["DataSourceID"] = value;
				base.DataSourceID = value;
			}
		}
		
		// 
		// See DataBoundControl.MarkAsDataBound msdn doc for the code example
		// 
		protected override void PerformSelect ()
		{
			// Call OnDataBinding here if bound to a data source using the
			// DataSource property (instead of a DataSourceID), because the
			// databinding statement is evaluated before the call to GetData.       
			if (!IsBoundUsingDataSourceID)
				OnDataBinding (EventArgs.Empty);

			// prevent recursive calls
			RequiresDataBinding = false;
			SelectArguments = CreateDataSourceSelectArguments ();
			GetData ().Select (SelectArguments, new DataSourceViewSelectCallback (OnSelect));

			// The PerformDataBinding method has completed.
			MarkAsDataBound ();
			
			// Raise the DataBound event.
			OnDataBound (EventArgs.Empty);
		}
		
		void OnSelect (IEnumerable data)
		{
			// Call OnDataBinding only if it has not already been 
			// called in the PerformSelect method.
			if (IsBoundUsingDataSourceID) {
				OnDataBinding (EventArgs.Empty);
			}
			// The PerformDataBinding method binds the data in the  
			// retrievedData collection to elements of the data-bound control.
			PerformDataBinding (data);
		}
		
		protected virtual DataSourceSelectArguments CreateDataSourceSelectArguments ()
		{
			return DataSourceSelectArguments.Empty;
		}
		
		protected DataSourceSelectArguments SelectArguments {
			get {
				if (selectArguments == null)
					selectArguments = CreateDataSourceSelectArguments ();
				return selectArguments;
			}
			private set {
				selectArguments = value;
			}
		}

		bool IsDataBound {
			get {
				object dataBound = ViewState ["DataBound"];
				return dataBound != null ? (bool) dataBound : false;
			}
			set {
				ViewState ["DataBound"] = value;
			}
		}

		protected void MarkAsDataBound ()
		{
			IsDataBound = true;
		}
	}
}
#endif





