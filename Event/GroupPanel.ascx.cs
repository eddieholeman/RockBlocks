﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using Newtonsoft.Json;
using Rock;
using Rock.Attribute;
using Rock.Constants;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web;
using Rock.Web.Cache;
using Rock.Web.UI;
using Rock.Web.UI.Controls;

namespace RockWeb.Plugins.com_kingdomfirstsolutions.Event
{
    public partial class KFSGroupPanel : System.Web.UI.UserControl
    {
        private Group _group;

        public Group Group
        {
            get { return _group; }
            set { _group = value; }
        }

        public event EventHandler AddButtonClick;

        public void BuildControl()
        {
            if ( _group != null )
            {
                BuildControl( _group );
            }
        }

        public void BuildControl ( Group group )
        {
            if ( _group == null )
            {
                _group = group;
            }
            // Set up group Panel widget
            pnlGroupDescription.Visible = !string.IsNullOrWhiteSpace( group.Description );
            lblGroupDescription.Text = group.Description;
            BuildSubgroupHeading( group );

            // Set up Member Grid
            RockContext rockContext = new RockContext();
            GroupMemberService groupMemberService = new GroupMemberService( rockContext );
            var qry = groupMemberService.Queryable( "Person,GroupRole", true ).AsNoTracking()
                .Where( m => m.GroupId == group.Id );
            List<GroupMember> groupMembersList = qry.ToList();
            gGroupMembers.DataKeyNames = new string[] { "Id" };
            gGroupMembers.Actions.AddClick += Actions_AddClick;
            gGroupMembers.Actions.ShowAdd = true;
            gGroupMembers.IsDeleteEnabled = true;
            //gGroupMembers.RowSelected += GGroupMembers_Edit;
            gGroupMembers.RowDataBound += GGroupMembers_RowDataBound;
            //gGroupMembers.RowCommand += GGroupMembers_RowCommand;

            // Add delete column
            var deleteField = new DeleteField();
            gGroupMembers.Columns.Add( deleteField );
            deleteField.Click += DeleteGroupMember_Click;

            gGroupMembers.DataSource = group.Members;
            gGroupMembers.DataBind();
        }

        public void RefreshMemberGrid( )
        {
            RockContext rockContext = new RockContext();
            GroupMemberService groupMemberService = new GroupMemberService( rockContext );
            var qry = groupMemberService.Queryable( "Person,GroupRole", true ).AsNoTracking()
                .Where( m => m.GroupId == _group.Id );
            List<GroupMember> groupMembersList = qry.ToList();
            gGroupMembers.DataSource = _group.Members;
            gGroupMembers.DataBind();
        }

        private void BuildSubgroupHeading( Group group )
        {
            pnlSubGroup.Title = string.Format( "<span class='span-panel-heading'>{0}</span>", group.Name );
            int memCount = group.Members.Where( m => m.GroupMemberStatus == GroupMemberStatus.Active || m.GroupMemberStatus == GroupMemberStatus.Pending ).Count();
            if ( group.GroupCapacity.HasValue && group.GroupCapacity > 0 )
            {
                string capacityRatio = string.Empty;
                if ( group.GroupCapacity == memCount )
                {
                    capacityRatio = string.Format( "&nbsp&nbsp<span class='label label-warning'>{0}/{1}</span>", memCount, group.GroupCapacity );
                }
                else if ( group.GroupCapacity < memCount )
                {
                    capacityRatio = string.Format( "&nbsp&nbsp<span class='label label-danger'>{0}/{1}</span>", memCount, group.GroupCapacity );
                }
                else
                {
                    capacityRatio = string.Format( "&nbsp&nbsp<span class='label label-success'>{0}/{1}</span>", memCount, group.GroupCapacity );
                }
                pnlSubGroup.Title += capacityRatio;
            }
        }

        private void Actions_AddClick( object sender, EventArgs e )
        {
            if ( AddButtonClick != null )
            {
                AddButtonClick( this, EventArgs.Empty );
            }
        }

        private void GGroupMembers_RowCommand( object sender, GridViewCommandEventArgs e )
        {
        }


        private void DeleteGroupMember_Click( object sender, RowEventArgs e )
        {
            RockContext rockContext = new RockContext();
            GroupMemberService groupMemberService = new GroupMemberService( rockContext );
            GroupMember groupMember = groupMemberService.Get( e.RowKeyId );
            if ( groupMember != null && groupMember.GroupId == _group.Id )
            {
                groupMemberService.Delete( groupMember );

                rockContext.SaveChanges();
                Group group = new GroupService( rockContext ).Get( groupMember.GroupId );
                gGroupMembers.DataSource = group.Members;
                gGroupMembers.DataBind();
                BuildSubgroupHeading( group );
            }
        }

        private void GGroupMembers_RowDataBound( object sender, GridViewRowEventArgs e )
        {
        }
    }
}