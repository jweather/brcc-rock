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
using System.Data;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text.RegularExpressions;

using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web;
using Rock.Web.Cache;

public partial class BRShortLinks : System.Web.UI.Page
{
    /// <summary>
    /// Handles the Init event of the Page control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    protected void Page_Init(object sender, EventArgs e)
    {
    }
    
    private void d(string msg) {
        debug.Text += "<br>" + msg;
    }
    
    private string canonicalize(string title) {
        string res = title.ToLower();
        
        Regex r = new Regex("[^a-z0-9 ]");
        res = r.Replace(res, "-");
        res = res.Replace(" ", "-");

        return res;
    }
        

    /// <summary>
    /// Handles the Load event of the Page control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    protected void Page_Load(object sender, EventArgs e)
    {
        // Jeremy Weatherford jweather@xidus.net
        // build a database table of shortlinks based on
        // - a Google Sheet with custom links
        // - Events and Groups with a ShortURL attribute (space-delimited for multiple entries)
        // - Posts get /posts/title (canonicalized title)
        // - Messages get /messages/title
        
        // these shortlinks are used by Http404Error.aspx.cs to redirect
        
        bool debug = true;
        try
        {
			Dictionary<string,string> links = new Dictionary<string,string>();
            List<string> duplicates = new List<string>();
            
            var rockContext = new RockContext();
            
            // fetch Google Sheet for custom shortlinks
            string sheetURL = "https://docs.google.com/spreadsheets/d/1Tc7E8tQ6wJjauYBKwDh1nOsfoVGK4zGci16tE91rp0U/export?format=csv";
            string csv = new WebClient().DownloadString(sheetURL);
            
            bool first = true;
            foreach (string line in csv.Split('\n')) {
                if (first) { first = false; continue; }
                var fields = line.Split(',');
                string link = fields[0].Trim(), url = fields[1].Trim();

                if (links.ContainsKey(link)) {
                    duplicates.Add(link + " duplicated by Google Sheet " + url);
                } else {
                    links.Add(link, url);
                }
            }            
            d("loaded " + links.Keys.Count() + " links from Google Sheets");
            
            // iterate Events with ShortURL attribute
            var attrs = new AttributeValueService(rockContext).Queryable()
                .Where( a => a.Attribute.Key == "ShortURL")
                .Where( a => a.Attribute.EntityTypeQualifierColumn == "EventCalendarId" );
            foreach (var attr in attrs) {
                string linkText = attr.Value.Trim();
                int id = attr.EntityId.Value;
                var item = new EventCalendarItemService(new RockContext()).Queryable()
                    .FirstOrDefault(i => i.Id == id);
                if (item != null && linkText != "") {
                    string url = "/page/505?EventItemId=" + item.EventItemId;
                    
                    foreach (string s in linkText.Split(' ')) {
                        string link = s;
                        if (link[0] != '/') link = '/' + link;
                        
                        if (links.ContainsKey(link)) {
                            duplicates.Add(link + " duplicated by Event " + id); // how to get event name?
                        } else {
                            links.Add(link, url);
                        }
                    }
                }
            }
            
            // iterate Messages
            var messages = new ContentChannelItemService(rockContext).Queryable()
                .Where(i => i.ContentChannelId == 5);
            foreach (var item in messages) {
                string link = "/messages/" + canonicalize(item.Title);
                if (links.ContainsKey(link)) {
                    duplicates.Add(link + " duplicated by Message " + item.Id + ": " + item.Title);
                } else {
                    string target = "/Page/498?Item=" + item.Id;
                    links.Add(link, target);
                }
            }

            // iterate Posts
            var posts = new ContentChannelItemService(rockContext).Queryable()
                .Where(i => i.ContentChannelId == 3);
            foreach (var item in posts) {
                string link = "/posts/" + canonicalize(item.Title);
                if (links.ContainsKey(link)) {
                    duplicates.Add(link + " duplicated by Post " + item.Id + ": " + item.Title);
                } else {
                    string target = "/Page/499?Item=" + item.Id;
                    links.Add(link, target);    
                }
            }
            
            // show error messages
            d("<p>");
            foreach (string msg in duplicates) {
                d("<font color=#f00>" + msg + "</font>");
            }
            d("<p>");
            
            d(links.Keys.Count() + " total links");
            
            // create BRShortLinks table if not exists
            var query = "IF NOT EXISTS (SELECT name FROM sysobjects WHERE name = 'BRShortLinks') " +
                "CREATE TABLE BRShortLinks(link nvarchar(250), url nvarchar(1024)," +
                "CONSTRAINT PK_BRShortLinks_Link PRIMARY KEY CLUSTERED (link))";
            DbService.ExecuteScaler(query);
            
            // pull existing shortlinks and compare
            List<string> deletes = new List<string>();
            List<string> adds = links.Keys.ToList();
            
            // iterate existing links and itemize changes
            var table = DbService.GetDataTable("SELECT link,url FROM BRShortLinks", CommandType.Text, null);
            d(table.Rows.Count + " existing links in DB");
            foreach (DataRow row in table.Rows) {
                string link = row[0].ToString(), url = row[1].ToString();
                if (!links.ContainsKey(link)) {
                    deletes.Add(link);
                } else if (links[link] == url) {
                    // no changes needed
                    adds.Remove(link);
                }
            }
            
            // update table, log changes
            foreach (string link in adds) {
                var p = new Dictionary<string,object>();
                p.Add("@link", link.ToLower());
                p.Add("@url", links[link]);
                int rows = DbService.ExecuteCommand("UPDATE BRShortLinks set [url]=@url WHERE [link]=@link;" +
                    "if @@ROWCOUNT = 0 INSERT INTO BRShortLinks ([link],[url]) VALUES (@link, @url)",
                    CommandType.Text, p, null);
                if (debug) d("inserting " + link + ": " + rows + " rows");
            }
            
            foreach (string link in deletes) {
                var p = new Dictionary<string,object>();
                p.Add("@link", link);
                DbService.ExecuteCommand("DELETE FROM BRShortLinks WHERE [link]=@link",
                    CommandType.Text, p, null);
                if (debug) d("deleting " + link);
            }
            
            d(adds.Count + " links added/updated, " + deletes.Count() + " links deleted");
            
            d("<p>");
            // debug dump of shortlink table
            foreach (var link in links.OrderBy(x => x.Key)) {
                d(link.Key + " -> " + link.Value);
            }
            
            // pet the watchdog
            var res = new WebClient().DownloadString("http://zenithav.net/watchdog.php?key=BRShortLinks" + Environment.MachineName);
        }
        catch (Exception e2)
        {
            d(e2.Message);
        }
    }
}