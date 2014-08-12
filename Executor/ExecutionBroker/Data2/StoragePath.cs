/* (с) НИУ ИТМО, 2011-2012 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Easis.Monitoring.DataLayer
{
    public sealed class StoragePath
    {
        private string _categoryPath;
        public string CategoryPath
        {
            get { return _categoryPath; }
            private set { _categoryPath = value; }
        }

        private IList<string> _categoryPathParts = null;
        public IList<string> CategoryPathParts
        {
            get { return _categoryPathParts; }
        }

        private string _queryString = null;
        public string QueryString
        {
            get { return _queryString; }
        }

        private string _objectId = null;
        public string ObjectId
        {
            get { return _objectId; }
        }

        public IList<string> FieldPathParts
        {
            get { return _fieldPathParts; }
        }
        private IList<string> _fieldPathParts = null;

        private string _fieldPath;
        public string FieldPath
        {
            get { return _fieldPath; }
            private set { _fieldPath = value; }
        }

        public const string CATEGORY_DELIMETER = ".";
        public const string OBJECT_ID_DELIMETER = "#";
        public const char QUERY_DELIMETER = ':';





        public StoragePath(string path)
        {
            if(String.IsNullOrEmpty(path))
            {
                return;
            }

            string[] delim = path.Split(OBJECT_ID_DELIMETER.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            if (delim.Length == 0)
            {
                throw new ArgumentException("Invalid storage path: " + path);
            }
            else if (delim.Length > 3)
            {
                throw new ArgumentException("Invalid storage path: " + path);
            }

            CategoryPath = delim[0];

            string[] strs = delim[0].Split(CATEGORY_DELIMETER.ToCharArray(), StringSplitOptions.None);
            _categoryPathParts = new List<string>();
            foreach (var str in strs)
            {
                _categoryPathParts.Add(str);
            }

            if (delim.Length >= 2)
            {

                if (delim[1].IndexOfAny(new char[] { ':', '{', '}', '"', '\'', '=' }) != -1)
                {
                    _queryString = delim[1];
                    _objectId = null;
                }
                else
                {
                    _objectId = delim[1];
                }
            }

            if (delim.Length == 3)
            {
                FieldPath = delim[2];
                strs = delim[2].Split(CATEGORY_DELIMETER.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                _fieldPathParts = new List<string>();
                foreach (var str in strs)
                {
                    _fieldPathParts.Add(str);
                }
            }

        }

        StoragePath()
        {
            _objectId = "";
            _categoryPathParts = new List<string>();
            _fieldPathParts = new List<string>();
        }

        public StoragePath(IList<string> categoryPathParts, string objectIdOrQueryString = null, IList<string> fieldPathParts = null)
        {
            _categoryPathParts = categoryPathParts;
            CategoryPath = String.Join(CATEGORY_DELIMETER, _categoryPathParts);

            if (objectIdOrQueryString.IndexOfAny(new char[] { ':', '{', '}', '"', '\'', '=' }) != -1)
            {
                _queryString = objectIdOrQueryString;
                _objectId = null;
            }
            else
            {
                _objectId = objectIdOrQueryString;
            }

            _fieldPathParts = fieldPathParts;
            if (fieldPathParts != null)
                FieldPath = String.Join(CATEGORY_DELIMETER, _fieldPathParts);
        }

        public override string ToString()
        {
            return String.Format("{0}{1}{2}{3}{4}", CategoryPath, OBJECT_ID_DELIMETER, ObjectId ?? QueryString,
                                 QUERY_DELIMETER, FieldPath);
        }
    }
}
