using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mapsui;
using Mapsui.UI.Android;
using Mapsui.Tiling;
using Mapsui.Utilities;
using Mapsui.UI;
using Mapsui.Styles;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using Mapsui.Projections;
using Mapsui.Nts;
using Mapsui.Extensions;
using Mapsui.Widgets.Zoom;
using NetTopologySuite.Geometries;

namespace Velociraptor
{
    internal class Map
    {
        public static MapControl? mapControl = null;

        public void CreateMap() 
        {
            //Display the map
            if (mapControl != null)
            {
                var map = new Mapsui.Map
                {
                    CRS = "EPSG:3857", //https://epsg.io/3857
                };
                map.Layers.Add(OpenStreetMap.CreateTileLayer());

                mapControl.Map = map;
                mapControl.Map.Navigator.RotationLock = true;
            }
        }
    }
}
