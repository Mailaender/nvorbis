﻿/* -*-mode:java; c-basic-offset:2; indent-tabs-mode:nil -*- */
/* JOrbis
 * Copyright (C) 2000 ymnk, JCraft,Inc.
 *  
 * Written by: 2000 ymnk<ymnk@jcraft.com>
 *   
 * Many thanks to 
 *   Monty <monty@xiph.org> and 
 *   The XIPHOPHORUS Company http://www.xiph.org/ .
 * JOrbis has been based on their awesome works, Vorbis codec.
 *   
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Library General Public License
 * as published by the Free Software Foundation; either version 2 of
 * the License, or (at your option) any later version.
   
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Library General Public License for more details.
 * 
 * You should have received a copy of the GNU Library General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NVorbis.jorbis
{
	// the comments are not part of vorbis_info so that vorbis_info can be
	// static storage
	public class Comment
	{
		private static byte[] _vorbis = Encoding.ASCII.GetBytes("vorbis");
		private static byte[] _vendor = Encoding.ASCII.GetBytes("Xiphophorus libVorbis I 20000508");

		private const int OV_EIMPL = -130;

		// unlimited user comment fields.
		public byte[][] user_comments;
		public int[] comment_lengths;
		public int comments;
		public byte[] vendor;

		public void init()
		{
			user_comments = null;
			comments = 0;
			vendor = null;
		}

		public void add(String comment)
		{
			add(Encoding.Default.GetBytes(comment));
		}

		private void add(byte[] comment)
		{
			byte[][] foo = new byte[comments + 2][];
			if (user_comments != null)
			{
				Array.Copy(user_comments, 0, foo, 0, comments);
			}
			user_comments = foo;

			int[] goo = new int[comments + 2];
			if (comment_lengths != null)
			{
				Array.Copy(comment_lengths, 0, goo, 0, comments);
			}
			comment_lengths = goo;

			byte[] bar = new byte[comment.Length + 1];
			Array.Copy(comment, 0, bar, 0, comment.Length);
			user_comments[comments] = bar;
			comment_lengths[comments] = comment.Length;
			comments++;
			user_comments[comments] = null;
		}

		public void add_tag(String tag, String contents)
		{
			if (contents == null)
				contents = "";
			add(tag + "=" + contents);
		}

		static bool tagcompare(byte[] s1, byte[] s2, int n)
		{
			int c = 0;
			byte u1, u2;
			while (c < n)
			{
				u1 = s1[c];
				u2 = s2[c];
				if ('Z' >= u1 && u1 >= 'A')
					u1 = (byte)(u1 - 'A' + 'a');
				if ('Z' >= u2 && u2 >= 'A')
					u2 = (byte)(u2 - 'A' + 'a');
				if (u1 != u2)
				{
					return false;
				}
				c++;
			}
			return true;
		}

		public String query(String tag)
		{
			return query(tag, 0);
		}

		public String query(String tag, int count)
		{
			int foo = query(Encoding.ASCII.GetBytes(tag), count);
			if (foo == -1)
				return null;
			byte[] comment = user_comments[foo];
			for (int i = 0; i < comment_lengths[foo]; i++)
			{
				if (comment[i] == '=')
				{

					return Util.InternalEncoding.GetString(comment, i + 1, comment_lengths[foo] - (i + 1));
				}
			}
			return null;
		}

		private int query(byte[] tag, int count)
		{
			int i = 0;
			int found = 0;
			int fulltaglen = tag.Length + 1;
			byte[] fulltag = new byte[fulltaglen];
			Array.Copy(tag, 0, fulltag, 0, tag.Length);
			fulltag[tag.Length] = (byte)'=';

			for (i = 0; i < comments; i++)
			{
				if (tagcompare(user_comments[i], fulltag, fulltaglen))
				{
					if (count == found)
					{
						// We return a pointer to the data, not a copy
						//return user_comments[i] + taglen + 1;
						return i;
					}
					else
					{
						found++;
					}
				}
			}
			return -1;
		}

		internal int unpack(NVorbis.jogg.Buffer opb)
		{
			int vendorlen = opb.Read(32);
			if (vendorlen < 0)
			{
				Clear();
				return (-1);
			}
			vendor = new byte[vendorlen + 1];
			opb.Read(vendor, vendorlen);
			comments = opb.Read(32);
			if (comments < 0)
			{
				Clear();
				return (-1);
			}
			user_comments = new byte[comments + 1][];
			comment_lengths = new int[comments + 1];

			for (int i = 0; i < comments; i++)
			{
				int len = opb.Read(32);
				if (len < 0)
				{
					Clear();
					return (-1);
				}
				comment_lengths[i] = len;
				user_comments[i] = new byte[len + 1];
				opb.Read(user_comments[i], len);
			}
			if (opb.Read(1) != 1)
			{
				Clear();
				return (-1);

			}
			return (0);
		}

		internal int Pack(NVorbis.jogg.Buffer Buffer)
		{
			// preamble
			Buffer.Write(0x03, 8);
			Buffer.Write(_vorbis);

			// vendor
			Buffer.Write(_vendor.Length, 32);
			Buffer.Write(_vendor);

			// comments
			Buffer.Write(comments, 32);
			if (comments != 0)
			{
				for (int i = 0; i < comments; i++)
				{
					if (user_comments[i] != null)
					{
						Buffer.Write(comment_lengths[i], 32);
						Buffer.Write(user_comments[i]);
					}
					else
					{
						Buffer.Write(0, 32);
					}
				}
			}
			Buffer.Write(1, 1);
			return (0);
		}

		public int HeaderOut(NVorbis.jogg.Packet op)
		{
			NVorbis.jogg.Buffer opb = new NVorbis.jogg.Buffer();
			opb.WriteInit();

			if (Pack(opb) != 0)
				return OV_EIMPL;

			op.packet_base = new byte[opb.bytes()];
			op.packet = 0;
			op.bytes = opb.bytes();
			Array.Copy(opb.buffer(), 0, op.packet_base, 0, op.bytes);
			op.b_o_s = 0;
			op.e_o_s = 0;
			op.granulepos = 0;
			return 0;
		}

		internal void Clear()
		{
			for (int i = 0; i < comments; i++)
				user_comments[i] = null;
			user_comments = null;
			vendor = null;
		}

		public String Vendor
		{
			get
			{
				return Util.InternalEncoding.GetString(vendor, 0, vendor.Length - 1);
			}
		}

		public String GetCommentAt(int i)
		{
			if (comments <= i)
				return null;
			return Util.InternalEncoding.GetString(user_comments[i], 0, user_comments[i].Length - 1);
		}

		public override string ToString()
		{
			String foo = "Vendor: " + Util.InternalEncoding.GetString(vendor, 0, vendor.Length - 1);
			for (int i = 0; i < comments; i++)
			{
				foo = foo + "\nComment: "
					+ Util.InternalEncoding.GetString(user_comments[i], 0, user_comments[i].Length - 1);
			}
			foo = foo + "\n";
			return foo;
		}
	}

}
