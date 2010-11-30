﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using System.Reflection;

namespace Hosts
{
	class HostNotSpecifiedException : ApplicationException { }
	class InvalidHostException : ApplicationException 
	{
		public string Host { get; protected set; }
		public InvalidHostException(string host) { Host = host; }
	}
	class InvalidIpException : ApplicationException
	{ 
		public string IP { get; protected set; }
		public InvalidIpException(string ip) { IP = ip; }
	}
	class HostNotFoundException : ApplicationException 
	{
		public string Host { get; protected set; }
		public HostNotFoundException(string host) { Host = host; }
	}

	class Program
	{
		static HostsEditor Hosts;

		static Program()
		{
			RegistryKey HostsRegKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\");
			string HostsPath = (string)HostsRegKey.GetValue("DataBasePath") + @"\hosts";
			HostsRegKey.Close();
			HostsPath = Environment.ExpandEnvironmentVariables(HostsPath);
			Hosts = new HostsEditor(HostsPath);
		}

		static T GetAssemblyAttribute<T>()
		{
			object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(T), false);
			return (attributes.Length == 0) ? default(T) : (T)attributes[0];
		}

		static string GetCopyright()
		{
			var CopyrightAttribute = GetAssemblyAttribute<AssemblyCopyrightAttribute>();
			return (CopyrightAttribute == null) ? String.Empty : CopyrightAttribute.Copyright;
		}

		static string GetTitle()
		{
			var TitleAttribute = GetAssemblyAttribute<AssemblyTitleAttribute>();
			return (TitleAttribute == null) ? String.Empty : TitleAttribute.Title;
		}

		static void Help()
		{
			Console.WriteLine(GetTitle());
			Console.WriteLine(GetCopyright());
			Console.WriteLine();
			Console.WriteLine("Usage:");
			Console.WriteLine("  hosts [add|set|new] <host> <addr> <comment>");
			Console.WriteLine("  hosts [rem|del]     <host|mask>");
			Console.WriteLine("  hosts [enable|on]   <host|mask>");
			Console.WriteLine("  hosts [disable|off] <host|mask>");
			Console.WriteLine("  hosts hide          <host|mask>");
			Console.WriteLine("  hosts show          <host|mask>");
			Console.WriteLine("  hosts [list|view]   [enabled|disabled] [visible|hidden] <mask>");
			Console.WriteLine("  hosts raw    - prints raw hosts file");
			Console.WriteLine("  hosts format - formats host rows");
			Console.WriteLine("  hosts clean  - removes all comments");
		}

		static void View(string mask, bool? visibleOnly = null, bool? enabledOnly = null)
		{
			int enabled = 0;
			int disabled = 0;
			int hidden = 0;

			if (mask != "*") Console.WriteLine("Mask: {0}\n", mask);

			Hosts.RemoveInvalid();
			Hosts.ResetFormat();
			List<HostLine> FoundLines = Hosts.GetMatch(mask);
			foreach (HostLine Line in FoundLines)
			{
				if (Line.Enabled) enabled++; else disabled++;
				if (Line.Hidden) hidden++;
				if (visibleOnly != null && visibleOnly.Value == Line.Hidden) continue;
				if (enabledOnly != null && enabledOnly.Value != Line.Enabled) continue;
				Console.WriteLine(Line);
			}
			if(FoundLines.Count > 0) Console.WriteLine();
			Console.WriteLine("Enabled: {0,-4} Disabled: {1,-4} Hidden: {2,-4}", enabled, disabled, hidden);
		}

		static void Main(string[] args)
		{
			try
			{
				Hosts.Load();

				if (args.Length == 0)
				{
					Console.WriteLine(GetTitle());
					Console.WriteLine("Usage: hosts < add | rem | on | off | view | help >");
					Console.WriteLine("Hosts file: " + Hosts.FileName.ToLower());
					Console.WriteLine();
					View("*", true, true);
					return;
				}
				
				List<HostLine> Lines;
				switch (args[0].ToLower().Trim())
				{
					case "raw":
					case "file":
						Console.WriteLine(File.ReadAllText(Hosts.FileName));
						return;

					case "print":
					case "list":
					case "view":
					case "select":
						bool? visibleOnly = null;
						bool? enabledOnly = null;
						string mask = "*";
						for (int i = 1; i < args.Length; i++)
						{
							string arg = args[i].ToLower();
							switch (arg)
							{
								case "enabled":		enabledOnly = true;		break;
								case "disabled":	enabledOnly = false;	break;
								case "hidden":		visibleOnly = false;	break;
								case "visible":		visibleOnly = true;		break;
								default:			mask = "*"+arg+"*";		break;
							}
						}
						View(mask, visibleOnly, enabledOnly);
						return;

					case "format":
						Hosts.ResetFormat();
						Console.WriteLine("Hosts file formatted successfully");
						break;

					case "clean":
						Hosts.RemoveInvalid();
						Hosts.ResetFormat();
						Console.WriteLine("Hosts file cleaned successfully");
						break;

					case "add":
					case "new":
					case "set":
					case "change":
					case "update":
						if (args.Length == 1) throw new HostNotSpecifiedException();
						if (!HostsEditor.CheckHost(args[1])) throw new InvalidHostException(args[1]);
						if (args.Length > 2 && !HostsEditor.CheckIP(args[2])) throw new InvalidIpException(args[2]);
						HostLine LineItem = Hosts.Get(args[1]);
						if (LineItem == null)
						{
							if (args.Length > 3) Hosts.Add(args[1], args[2], args[3]);
							else if (args.Length > 2) Hosts.Add(args[1], args[2]);
							else Hosts.Add(args[1], "127.0.0.1");
							LineItem = Hosts.Get(args[1]);
							Console.WriteLine("[ADDED] {0} {1}", LineItem.Host, LineItem.IP);
						}
						else
						{
							if (args.Length > 2) LineItem.IP = args[2];
							if (args.Length > 3) LineItem.Comment = args[3];
							Console.WriteLine("[UPDATED] {0} {1}", LineItem.Host, LineItem.IP);
						}
						break;

					case "rem":
					case "remove":
					case "del":
					case "delete":
						if (args.Length == 1) throw new HostNotSpecifiedException();
						Lines = Hosts.GetMatch(args[1]);
						if (Lines.Count == 0) throw new HostNotFoundException(args[1]);
						foreach (HostLine Line in Lines)
						{
							string host = Line.Host;
							Hosts.Remove(host);
							Console.WriteLine("[REMOVED] {0}", host);
						}
						break;

					case "on":
					case "enable":
						if (args.Length == 1) throw new HostNotSpecifiedException();
						Lines = Hosts.GetMatch(args[1]);
						if (Lines.Count == 0) throw new HostNotFoundException(args[1]);
						foreach (HostLine Line in Lines)
						{
							Line.Enabled = true;
							Console.WriteLine("[ENABLED] {0} {1}", Line.Host, Line.IP);
						}
						break;

					case "off":
					case "disable":
						if (args.Length == 1) throw new HostNotSpecifiedException();
						Lines = Hosts.GetMatch(args[1]);
						if (Lines.Count == 0) throw new HostNotFoundException(args[1]);
						foreach (HostLine Line in Lines)
						{
							Line.Enabled = false;
							Console.WriteLine("[DISABLED] {0} {1}", Line.Host, Line.IP);
						}
						break;

					case "hide":
						if (args.Length == 1) throw new HostNotSpecifiedException();
						Lines = Hosts.GetMatch(args[1]);
						if (Lines.Count == 0) throw new HostNotFoundException(args[1]);
						foreach (HostLine Line in Lines)
						{
							Line.Hidden = true;
							Console.WriteLine("[HIDDEN] {0} {1}", Line.Host, Line.IP);
						}
						break;

					case "show":
						if (args.Length == 1) throw new HostNotSpecifiedException();
						Lines = Hosts.GetMatch(args[1]);
						if (Lines.Count == 0) throw new HostNotFoundException(args[1]);
						foreach (HostLine Line in Lines)
						{
							Line.Hidden = false;
							Console.WriteLine("[SHOWN] {0} {1}", Line.Host, Line.IP);
						}
						break;

					default:
					case "help":
						Help();
						return;
				}
				Hosts.Save();
			}
			catch (HostNotSpecifiedException)
			{
				Console.WriteLine("[ERROR] Host not specified");
			}
			catch (HostNotFoundException e)
			{
				Console.WriteLine("[ERROR] Host '{0}' not found", e.Host);
			}
			catch (InvalidHostException e)
			{
				Console.WriteLine("[ERROR] Invalid host '{0}'", e.Host);
			}
			catch (InvalidIpException e)
			{
				Console.WriteLine("[ERROR] Invalid IP '{0}'", e.IP);
			}
			catch (Exception e)
			{
				Console.WriteLine("[ERROR] " + e.Message);
			}
		}
	}
}
