﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
	
namespace Easis.Eventing
{
	public class EventReportSerializer
	{
	
	    /// <summary>
	    /// To convert a Byte Array of Unicode values (UTF-8 encoded) to a complete String.
	    /// </summary>
	    /// <param name="characters">Unicode Byte Array to be converted to String</param>
	    /// <returns>String converted from Unicode Byte Array</returns>
	    private static String UTF8ByteArrayToString(Byte[] characters)
	    {
	        UTF8Encoding encoding = new UTF8Encoding();
	        String constructedString = encoding.GetString(characters);
	        return (constructedString);
	    }
	
	    /// <summary>
	    /// Converts the String to UTF8 Byte array and is used in De serialization
	    /// </summary>
	    /// <param name="pXmlString"></param>
	    /// <returns></returns>
	    private static Byte[] StringToUTF8ByteArray(String pXmlString)
	    {
	        UTF8Encoding encoding = new UTF8Encoding();
	        Byte[] byteArray = encoding.GetBytes(pXmlString);
	        return byteArray;
	    }
	
	    /// <summary>
	    /// Serializes eventreport object into xml with utf8 encoding.
	    /// </summary>
	    /// <param name="pObject"></param>
	    /// <param name="type"></param>
	    /// <returns></returns>
	    public static String SerializeObject(Object pObject, System.Type type)
	    {
	        try
	        {
	            String XmlizedString = null;
	            MemoryStream memoryStream = new MemoryStream();
	            XmlSerializer xs = new XmlSerializer(type);
	            XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
	            xs.Serialize(xmlTextWriter, pObject);
	            memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
	            XmlizedString = UTF8ByteArrayToString(memoryStream.ToArray());
	            return XmlizedString;
	        }
	        catch (Exception e) 
	        {
	            throw e; // todo: "throw;" vs "throw e;"
	        }
	    }
	
	    /// <summary>
	    /// Method to reconstruct an Object from XML string
	    /// </summary>
	    /// <param name="pXmlizedString"></param>
	    /// <returns></returns>
	    public Object DeserializeObject(String pXmlizedString, System.Type type)
	    {
	        XmlSerializer xs = new XmlSerializer(type);
	        MemoryStream memoryStream = new MemoryStream(StringToUTF8ByteArray(pXmlizedString));
	        XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
	        return xs.Deserialize(memoryStream);
	    }
	
	
	}
}