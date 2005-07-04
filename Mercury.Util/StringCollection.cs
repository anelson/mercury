using System;
using System.Collections;

namespace Mercury.Util
{
	/// <summary>
	/// Summary description for StringCollection.
	/// </summary>
	public class StringCollection : CollectionBase {
		public String this[ int index ]  {
			get  {
				return( (String) List[index] );
			}
			set  {
				List[index] = value;
			}
		}

		public int Add( String value ) {
			return( List.Add( value ) );
		}

		public int IndexOf( String value ) {
			return( List.IndexOf( value ) );
		}

		public void Insert( int index, String value ) {
			List.Insert( index, value );
		}

		public void Remove( String value ) {
			List.Remove( value );
		}

		public bool Contains( String value ) {
			// If value is not of type String, this will return false.
			return( List.Contains( value ) );
		}

		protected override void OnInsert( int index, Object value ) {
			if ( value.GetType() != typeof(String))
				throw new ArgumentException( "value must be of type String.", "value" );
		}

		protected override void OnRemove( int index, Object value ) {
			if ( value.GetType() != typeof(String))
				throw new ArgumentException( "value must be of type String.", "value" );
		}

		protected override void OnSet( int index, Object oldValue, Object newValue ) {
			if ( newValue.GetType() != typeof(String))
				throw new ArgumentException( "newValue must be of type String.", "newValue" );
		}

		protected override void OnValidate( Object value ) {
			if ( value.GetType() != typeof(String))
				throw new ArgumentException( "value must be of type String." );
		}

	}

}
