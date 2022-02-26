using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ExifName
{
	/// <summary>
	/// Configuration element collection for <see cref="ExifConfigurationElement"/>
	/// </summary>
	public class ExifConfigurationElementCollection : ConfigurationElementCollection
	{
		/// <summary>
		/// Get the new element.
		/// </summary>
		/// <returns>A <see cref="ExifConfigurationElement"/> instance.</returns>
		protected override ConfigurationElement CreateNewElement()
		{
			return new ExifConfigurationElement();
		}

		/// <summary>
		/// Get the element key.
		/// </summary>
		/// <param name="element">The element.</param>
		/// <returns>A key of the element or empty string.</returns>
		protected override object GetElementKey( ConfigurationElement element )
		{
			ExifConfigurationElement customElement = element as ExifConfigurationElement;

			if( customElement != null )
			{
				return customElement.Ext + customElement.Camera + customElement.Owner;
			}

			return String.Empty;
		}

		/// <summary>
		/// Gets or sets <see cref="ExifConfigurationElement"/> at specified index.
		/// </summary>
		public ExifConfigurationElement this[ int index ]
		{
			get { return base.BaseGet( index ) as ExifConfigurationElement; }
			set
			{
				if( base.BaseGet( index ) != null )
				{
					base.BaseRemoveAt( index );
				}

				base.BaseAdd( index, value );
			}
		}

		public int IndexOf( ExifConfigurationElement element )
		{
			return BaseIndexOf( element );
		}

		public void Add( ExifConfigurationElement element )
		{
			BaseAdd( element );
		}
		protected override void BaseAdd( ConfigurationElement element )
		{
			BaseAdd( element, false );
		}

		public void Remove( ExifConfigurationElement element )
		{
			if( BaseIndexOf( element ) >= 0 )
				BaseRemove( GetElementKey( element ) );
		}

		public void RemoveAt( int index )
		{
			BaseRemoveAt( index );
		}

		public void Remove( string name )
		{
			BaseRemove( name );
		}

		public void Clear()
		{
			BaseClear();
		}
	}

	/// <summary>
	/// Custom configuration section to contain the configuration element collection.
	/// </summary>
	public class ExifConfigurationSection : ConfigurationSection
	{
		/// <summary>
		/// Gets the collection of custom configuration elements.
		/// </summary>
		// Declare the Urls collection property using the
		// ConfigurationCollectionAttribute.
		// This allows to build a nested section that contains
		// a collection of elements.
		[ConfigurationProperty( "files", IsDefaultCollection = false )]
		[ConfigurationCollection( typeof( ExifConfigurationSection ),
			AddItemName = "add",
			ClearItemsName = "clear",
			RemoveItemName = "remove" )]
		public ExifConfigurationElementCollection Files
		{
			get
			{
				ExifConfigurationElementCollection collection = base[ "files" ] as ExifConfigurationElementCollection;

				return collection ?? new ExifConfigurationElementCollection();
			}
		}
	}

	/// <summary>
	/// Custom configuration element.
	/// </summary>
	public class ExifConfigurationElement : ConfigurationElement
	{
		/// <summary>
		/// Gets the configuration key.
		/// </summary>
		[ConfigurationProperty( "ext", IsRequired = true )]
		public string Ext
		{
			get
			{
				string value = base[ "ext" ] as String;

				return value ?? String.Empty;
			}
		}

		/// <summary>
		/// Gets the value of camera attribute.
		/// </summary>
		[ConfigurationProperty( "camera", IsRequired = false )]
		public string Camera
		{
			get
			{
				string value = base[ "camera" ] as String;

				return value ?? String.Empty;
			}
		}

		/// <summary>
		/// Gets the value of owner attribute.
		/// </summary>
		[ConfigurationProperty( "owner", IsRequired = false )]
		public string Owner
		{
			get
			{
				string value = base[ "owner" ] as String;

				return value ?? String.Empty;
			}
		}

		/// <summary>
		/// Gets the value of offset attribute, default is 0.
		/// </summary>
		[ConfigurationProperty( "offset", IsRequired = false, DefaultValue = null )]
		public TimeSpan Offset
		{
			get
			{
				object value = base[ "offset" ];
				if( value == null ) return new TimeSpan( 0 );

				return (TimeSpan)value;
			}
		}

		/// <summary>
		/// Gets the value of min attribute.
		/// </summary>
		[ConfigurationProperty( "min", IsRequired = true )]
		public DateTime Min
		{
			get
			{
				object value = base[ "min" ];
				if( value == null ) throw new Exception( "Не задана минимальная дата в параметре конфигурации" );

				string str = Convert.ToString( value );
				DateTime result;

				if( DateTime.TryParse( str, out result ) )
					return result;
				else
					throw new Exception( $"Значение параметра равное {str} в конфигурации не распознано как дата" );
			}
		}

		/// <summary>
		/// Gets the value of min attribute.
		/// </summary>
		[ConfigurationProperty( "max", IsRequired = true )]
		public DateTime Max
		{
			get
			{
				object value = base[ "max" ];
				if( value == null ) throw new Exception( "Не задана максимальная дата в параметре конфигурации" );

				string str = Convert.ToString( value );
				DateTime result;

				if( DateTime.TryParse( str, out result ) )
					return result;
				else
					throw new Exception( $"Значение параметра равное {str} в конфигурации не распознано как дата" );
			}
		}
	}
}
