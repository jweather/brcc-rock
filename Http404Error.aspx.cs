// <copyright>
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web;
using Rock.Web.Cache;

public partial class Http404Error : System.Web.UI.Page
{
    /// <summary>
    /// Handles the Init event of the Page control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    protected void Page_Init(object sender, EventArgs e)
    {
        // Check to see if exception should be logged
        if ( GlobalAttributesCache.Read().GetValue( "Log404AsException" ).AsBoolean(true) )
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
    protected void Page_Load(object sender, EventArgs e)
    {
        try
        {
            // Set form action to pass XSS test
            form1.Action = "/";
        
            // try to get site's 404 page
            SiteCache site = SiteCache.GetSiteByDomain(Request.Url.Host);
            if ( site != null && site.PageNotFoundPageId.HasValue )
            {
                site.RedirectToPageNotFoundPage();
            }
            else
            {
                Response.StatusCode = 404;
                lLogoSvg.Text = System.IO.File.ReadAllText( HttpContext.Current.Request.MapPath( "~/Assets/Images/rock-logo-sm.svg" ) );
				
				string search = Request.RawUrl.Substring(1).ToLower();
				
				RockContext rockContext = new RockContext();
				
				int? idp = new AttributeValueService( rockContext ).Queryable()
					.Where( a => a.Attribute.Key == "ShortURL")
					.Where( a => a.Attribute.EntityTypeQualifierColumn == "ContentChannelTypeId")
					.Where( a => a.Value == search )
					.Select( a => a.EntityId ).FirstOrDefault();
				
				if (!idp.HasValue) {
					// an actual 404
				} else {
					int id = idp.Value;
					
					ContentChannelItemService ccis = new ContentChannelItemService(new RockContext());
					ContentChannelItem item = ccis.Queryable().FirstOrDefault(i => i.Id == id);
					if (item == null) {
						// odd
					} else {
						// magic numbers ahoy
						int page = 0;
						if (item.ContentChannelId == 5) page = 506; // messages
						else if (item.ContentChannelId == 3) page = 499; // stories
						
						if (page != 0) {
							string url = "/page/" + page + "?Item=" + item.Id; // magic values ahoy
						
							Response.Redirect(url, false);
							return;
						}
					}
				}
            }
			
			// really 404
			Response.Redirect("/page/504", false);
        }
        catch (Exception e2)
        {
            Response.Redirect("/page/504", false);
        }
    }
}