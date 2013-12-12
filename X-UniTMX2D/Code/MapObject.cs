/*!
 * X-UniTMX: A tiled map editor file importer for Unity3d
 * https://bitbucket.org/Chaoseiro/x-unitmx
 * 
 * Copyright 2013 Guilherme "Chaoseiro" Maia
 * Released under the MIT license
 * Check LICENSE.MIT for more details.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Globalization;
using UnityEngine;

namespace X_UniTMX
{
	/// <summary>
	/// Map Object Type, from Tiled's Objects types
	/// </summary>
	public enum MapObjectType : byte
	{
		Box,	
		Ellipse,
		Polygon,
		Polyline
	}

	/// <summary>
	/// An arbitrary object placed on an ObjectLayer.
	/// </summary>
	public class MapObject
	{
		/// <summary>
		/// Gets the name of the object.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Gets the type of the object.
		/// </summary>
		public string Type { get; private set; }

		/// <summary>
		/// Gets the mapobjecttype of the object.
		/// </summary>
		public MapObjectType MapObjectType { get; private set; }

		/// <summary>
		/// Gets or sets the bounds of the object.
		/// </summary>
		public Rect Bounds { get; set; }

		/// <summary>
		/// Gets a list of the object's properties.
		/// </summary>
		public PropertyCollection Properties { get; private set; }

		/// <summary>
		/// Gets the object GID
		/// </summary>
		public int GID { get; private set; }

		/// <summary>
		/// Gets a list of the object's points
		/// </summary>
		public List<Vector2> Points { get; private set; }

		/// <summary>
		/// Creates a new MapObject.
		/// </summary>
		/// <param name="name">The name of the object.</param>
		/// <param name="type">The type of object to create.</param>
		public MapObject(string name, string type) : this(name, type, new Rect(), new PropertyCollection(), 0, new List<Vector2>()) { }

		/// <summary>
		/// Creates a new MapObject.
		/// </summary>
		/// <param name="name">The name of the object.</param>
		/// <param name="type">The type of object to create.</param>
		/// <param name="bounds">The initial bounds of the object.</param>
		public MapObject(string name, string type, Rect bounds) : this(name, type, bounds, new PropertyCollection(), 0, new List<Vector2>()) { }

		/// <summary>
		/// Creates a new MapObject.
		/// </summary>
		/// <param name="name">The name of the object.</param>
		/// <param name="type">The type of object to create.</param>
		/// <param name="bounds">The initial bounds of the object.</param>
		/// <param name="properties">The initial property collection or null to create an empty property collection.</param>
		public MapObject(string name, string type, Rect bounds, PropertyCollection properties, int gid, List<Vector2> points)
		{
			if (string.IsNullOrEmpty(name))
				throw new ArgumentException(null, "name");

			Name = name;
			Type = type;
			Bounds = bounds;
			Properties = properties ?? new PropertyCollection();
			GID = gid;
			Points = points;
		}

		public MapObject(XmlNode node)
        {
			if (node.Attributes["name"] != null)
			{
				Name = node.Attributes["name"].Value;
			}
			else
			{
				Name = "Object";
			}

            if (node.Attributes["type"] != null)
            {
                Type = node.Attributes["type"].Value;
            }

            // values default to 0 if the attribute is missing from the node
            int x = node.Attributes["x"] != null ? int.Parse(node.Attributes["x"].Value, CultureInfo.InvariantCulture) : 0;
            int y = node.Attributes["y"] != null ? int.Parse(node.Attributes["y"].Value, CultureInfo.InvariantCulture) : 0;
            int width = node.Attributes["width"] != null ? int.Parse(node.Attributes["width"].Value, CultureInfo.InvariantCulture) : 0;
            int height = node.Attributes["height"] != null ? int.Parse(node.Attributes["height"].Value, CultureInfo.InvariantCulture) : 0;

            Bounds = new Rect(x, y, width, height);

			MapObjectType = MapObjectType.Box;

            XmlNode propertiesNode = node["properties"];
            if (propertiesNode != null)
            {
                Properties = new PropertyCollection(propertiesNode);
            }

            // stores a string of points to parse out if this object is a polygon or polyline
            string pointsAsString = null;

            // if there's a GID, it's a tile object
            if (node.Attributes["gid"] != null)
            {
                //ObjectType = MapObjectType.Tile;
                GID = int.Parse(node.Attributes["gid"].Value, CultureInfo.InvariantCulture);
            }
			// if there's an ellipse node, it's an ellipse object
			else if (node["ellipse"] != null)
			{
				MapObjectType = MapObjectType.Ellipse;
			}
			// if there's a polygon node, it's a polygon object
			else if (node["polygon"] != null)
			{
				//ObjectType = MapObjectType.Polygon;
				pointsAsString = node["polygon"].Attributes["points"].Value;
				MapObjectType = MapObjectType.Polygon;
			}
			// if there's a polyline node, it's a polyline object
			else if (node["polyline"] != null)
			{
				//ObjectType = MapObjectType.Polyline;
				pointsAsString = node["polyline"].Attributes["points"].Value;
				MapObjectType = MapObjectType.Polyline;
			}

            // if we have some points to parse, we do that now
            if (pointsAsString != null)
            {
                // points are separated first by spaces
				Points = new List<Vector2>();
                string[] pointPairs = pointsAsString.Split(' ');
                foreach (string p in pointPairs)
                {
                    // then we split on commas
                    string[] coords = p.Split(',');

                    // then we parse the X/Y coordinates
                    Points.Add(new Vector2(
                        float.Parse(coords[0], CultureInfo.InvariantCulture),
                        float.Parse(coords[1], CultureInfo.InvariantCulture)));
                }
            }
        }

		public void ScaleObject(float TileWidth, float TileHeight)
		{
			this.Bounds = new Rect(this.Bounds.x / TileWidth, this.Bounds.y / TileHeight, this.Bounds.width / TileWidth, this.Bounds.height / TileHeight);
			
			if (this.Points != null)
			{
				for (int i = 0; i < this.Points.Count; i++)
				{
					this.Points[i] = new Vector2(this.Points[i].x / TileWidth, this.Points[i].y / TileHeight);
				}
			}
		}

		/// <summary>
		/// Gets a string property
		/// </summary>
		/// <param name="property">Name of the property inside Tiled</param>
		/// <returns>The value of the property, String.Empty if property not found</returns>
		public string GetPropertyAsString(string property)
		{
			string str = string.Empty;
			Property p = null;
			if (Properties == null)
				return str;
			if (Properties.TryGetValue(property.ToLowerInvariant(), out p))
				str = p.RawValue;

			return str;
		}
		/// <summary>
		/// Gets a boolean property
		/// </summary>
		/// <param name="property">Name of the property inside Tiled</param>
		/// <returns>The value of the property</returns>
		public bool GetPropertyAsBoolean(string property)
		{
			bool b = false;
			string str = string.Empty;
			Property p = null;
			if (Properties == null)
				return b;
			if (Properties.TryGetValue(property.ToLowerInvariant(), out p))
				str = p.RawValue;

			Boolean.TryParse(str, out b);

			return b;
		}
		/// <summary>
		/// Gets an integer property
		/// </summary>
		/// <param name="property">Name of the property inside Tiled</param>
		/// <returns>The value of the property</returns>
		public int GetPropertyAsInt(string property)
		{
			int b = 0;
			string str = string.Empty;
			Property p = null;
			if (Properties == null)
				return b;
			if (Properties.TryGetValue(property.ToLowerInvariant(), out p))
				str = p.RawValue;

			Int32.TryParse(str, out b);

			return b;
		}
		/// <summary>
		/// Gets a float property
		/// </summary>
		/// <param name="property">Name of the property inside Tiled</param>
		/// <returns>The value of the property</returns>
		public float GetPropertyAsFloat(string property)
		{
			float b = 0;
			string str = string.Empty;
			Property p = null;
			if (Properties == null)
				return b;
			if (Properties.TryGetValue(property.ToLowerInvariant(), out p))
				str = p.RawValue;

			float.TryParse(str, out b);

			return b;
		}
	}
}
