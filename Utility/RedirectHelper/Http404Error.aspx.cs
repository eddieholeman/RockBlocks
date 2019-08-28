﻿// <copyright>
// Copyright by the Spark Development Network
//
// Licensed under the Rock Community License (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.rockrms.com/license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//
// <notice>
// This file contains modifications by Kingdom First Solutions
// and is a derivative work.
//
// Modification (including but not limited to):
// * Add a query string of originalUrl to the landing 404 page.
// </notice>
//
using System;
using System.Web;

using Rock;
using Rock.Model;
using Rock.Web;
using Rock.Web.Cache;

namespace RockWeb.Plugins.rocks_kfs.Utility
{
    public partial class Http404Error : System.Web.UI.Page
    {
        /// <summary>
        /// Handles the Init event of the Page control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Page_Init( object sender, EventArgs e )
        {
            // Check to see if exception should be logged
            if ( GlobalAttributesCache.Get().GetValue( "Log404AsException" ).AsBoolean( true ) )
            {
                ExceptionLogService.LogException( new Exception( string.Format( "404 Error: {0}", Request.Url.AbsoluteUri ) ), Context );
            }

            // If this is an API call, set status code and exit
            if ( Request.Url.Query.Contains( Request.Url.Authority + ResolveUrl( "~/api/" ) ) )
            {
                Response.StatusCode = 404;
                Response.Flush();
                Response.End();
                return;
            }
        }

        /// <summary>
        /// Handles the Load event of the Page control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Page_Load( object sender, EventArgs e )
        {
            try
            {
                // Set form action to pass XSS test
                form1.Action = "/";

                // try to get site's 404 page
                SiteCache site = SiteCache.GetSiteByDomain( Request.Url.Host );
                if ( site != null && site.PageNotFoundPageId.HasValue )
                {
                    //site.RedirectToPageNotFoundPage();
                    PageReference redirectRef = site.PageNotFoundPageReference;

                    if ( redirectRef.QueryString == null )
                    {
                        redirectRef.QueryString = new System.Collections.Specialized.NameValueCollection();
                    }
                    string orignalUrl = Request.RawUrl.IndexOf( '/' ) == 0 ? Request.RawUrl.Substring( 1 ) : Request.RawUrl;
                    redirectRef.QueryString.Add( "originalUrl", orignalUrl );

                    Response.Redirect( redirectRef.BuildUrl(), false );
                }
                else
                {
                    Response.StatusCode = 404;
                    lLogoSvg.Text = System.IO.File.ReadAllText( HttpContext.Current.Request.MapPath( "~/Assets/Images/rock-logo-sm.svg" ) );
                }
            }
            catch
            {
                Response.StatusCode = 404;
                lLogoSvg.Text = System.IO.File.ReadAllText( HttpContext.Current.Request.MapPath( "~/Assets/Images/rock-logo-sm.svg" ) );
            }
        }
    }
}
