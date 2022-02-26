using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;

// Используется MetadataExtractor, https://github.com/drewnoakes/metadata-extractor-dotnet

namespace ExifName
{
	class Program
	{

		static void Main( string[] args )
		{
			// Необходимодля рагрузки зависимых сборок из ресурсов
			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

			Processor processor = new Processor();
			processor.Run( args );

			//Console.ReadKey();
		}

		private static Assembly CurrentDomain_AssemblyResolve( object sender, ResolveEventArgs args )
		{
			// Source: https://stackoverflow.com/questions/10137937/merge-dll-into-exe

			Assembly thisAssembly = Assembly.GetExecutingAssembly();

			return AssemblyResolve( args.Name );
		}

		private static Assembly AssemblyResolve( string name )
		{
			// Source: https://stackoverflow.com/questions/10137937/merge-dll-into-exe

			Assembly thisAssembly = Assembly.GetExecutingAssembly();

			//Get the Name of the AssemblyFile
			if( !name.EndsWith( ".dll" ) )
			{
				int index = name.IndexOf( ',' );
				if( index > 0 )
					name = name.Substring( 0, index );
				name += ".dll";
			}

			//Load form Embedded Resources - This Function is not called if the Assembly is in the Application Folder
			var resources = thisAssembly.GetManifestResourceNames().Where( s => s.EndsWith( name ) );
			if( resources.Count() > 0 )
			{
				var resourceName = resources.First();
				using( Stream stream = thisAssembly.GetManifestResourceStream( resourceName ) )
				{
					if( stream == null ) return null;
					var block = new byte[ stream.Length ];
					stream.Read( block, 0, block.Length );
					Assembly asm = Assembly.Load( block );
					AssemblyName[] asmNames = asm.GetReferencedAssemblies();
					return asm;
				}
			}
			return null;
		}
	}
}
