/*
  This file is licensed to You under the Apache License, Version 2.0
  (the "License"); you may not use this file except in compliance with
  the License.  You may obtain a copy of the License at

  http://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Org.XmlUnit.Util;

namespace Org.XmlUnit.Diff {

    /// <summary>
    /// Common ElementSelector implementations.
    /// </summary>
    public sealed class ElementSelectors {
        private ElementSelectors() { }

        /// <summary>
        /// Always returns true, i.e. each element can be compared to each
        /// other element.
        /// </summary>
        /// <remarks>
        /// Generally this means elements will be compared in document
        /// order.
        /// </remarks>
        public static bool Default(XmlElement controlElement,
                                   XmlElement testElement) {
            return true;
        }

        /// <summary>
        /// Elements with the same local name (and namespace URI - if any)
        /// can be compared.
        /// </summary>
        public static bool ByName(XmlElement controlElement,
                                  XmlElement testElement) {
            return controlElement != null && testElement != null
                && BothNullOrEqual(Nodes.GetQName(controlElement),
                                   Nodes.GetQName(testElement));
        }

        /// <summary>
        /// Elements with the same local name (and namespace URI - if any)
        /// and nested text (if any) can be compared.
        /// </summary>
        public static bool ByNameAndText(XmlElement controlElement,
                                         XmlElement testElement) {
            return ByName(controlElement, testElement)
                && BothNullOrEqual(Nodes.GetMergedNestedText(controlElement),
                                   Nodes.GetMergedNestedText(testElement));
        }

        /// <summary>
        /// Elements with the same local name (and namespace URI - if any)
        /// and attribute values for the given attribute names can be
        /// compared.
        /// </summary>
        /// <remarks>Attributes are only searched for in the null
        /// namespace.</remarks>
        public static ElementSelector
        ByNameAndAttributes(params string[] attribs) {
            if (attribs == null) {
                throw new ArgumentNullException("attribs");
            }
            return ByNameAndAttributes(attribs
                                       .Select(a => new XmlQualifiedName(a))
                                       .ToArray());
        }

        /// <summary>
        /// Elements with the same local name (and namespace URI - if any)
        /// and attribute values for the given attribute names can be
        /// compared.
        /// </summary>
        public static ElementSelector
            ByNameAndAttributes(params XmlQualifiedName[] attribs) {
            if (attribs == null) {
                throw new ArgumentNullException("attribs");
            }
            XmlQualifiedName[] qs = new XmlQualifiedName[attribs.Length];
            Array.Copy(attribs, 0, qs, 0, qs.Length);
            return (controlElement, testElement) =>
                   ByName(controlElement, testElement) &&
                   MapsEqualForKeys(Nodes.GetAttributes(controlElement),
                                    Nodes.GetAttributes(testElement),
                                    qs);
        }

        /// <summary>
        /// Elements with the same local name (and namespace URI - if any)
        /// and attribute values for the given attribute names can be
        /// compared.
        /// </summary>
        /// <remarks>
        /// Namespace URIs of attributes are those of the attributes on
        /// the control element or the null namespace if they don't
        /// exist.
        /// </remarks>
        public static ElementSelector
            ByNameAndAttributesControlNS(params string[] attribs) {
            if (attribs == null) {
                throw new ArgumentNullException("attribs");
            }
            ISet<string> ats = new HashSet<string>(attribs);
            return (controlElement, testElement) => {
                if (!ByName(controlElement, testElement)) {
                    return false;
                }
                IDictionary<XmlQualifiedName, string> cAttrs =
                    Nodes.GetAttributes(controlElement);
                IDictionary<string, XmlQualifiedName> qNameByLocalName =
                    new Dictionary<string, XmlQualifiedName>();
                foreach (XmlQualifiedName q in cAttrs.Keys) {
                    string local = q.Name;
                    if (ats.Contains(local)) {
                        qNameByLocalName[local] = q;
                    }
                }
                foreach (string a in ats) {
                    if (!qNameByLocalName.ContainsKey(a)) {
                        qNameByLocalName[a] = new XmlQualifiedName(a);
                    }
                }
                return MapsEqualForKeys(cAttrs,
                                        Nodes.GetAttributes(testElement),
                                        qNameByLocalName.Values);
            };
        }

        /// <summary>
        /// Elements with the same local name (and namespace URI - if any)
        /// and attribute values for all attributes can be compared.
        /// </summary>
        public static bool ByNameAndAllAttributes(XmlElement controlElement,
                                                  XmlElement testElement) {
            if (!ByName(controlElement, testElement)) {
                return false;
            }
            IDictionary<XmlQualifiedName, string> cAttrs =
                Nodes.GetAttributes(controlElement);
            IDictionary<XmlQualifiedName, string> tAttrs =
                Nodes.GetAttributes(testElement);
            if (cAttrs.Count != tAttrs.Count) {
                return false;
            }
            return MapsEqualForKeys(cAttrs, tAttrs, cAttrs.Keys);
        }

        /// <summary>
        /// Elements with the same local name (and namespace URI - if any)
        /// and child elements and nested text at each level (if any) can
        /// be compared.
        /// </summary>
        public static bool ByNameAndTextRec(XmlElement controlElement,
                                            XmlElement testElement) {
            if (!ByNameAndText(controlElement, testElement)) {
                return false;
            }

            XmlNodeList controlChildren = controlElement.ChildNodes;
            XmlNodeList testChildren = testElement.ChildNodes;
            int controlLen = controlChildren.Count;
            int testLen = testChildren.Count;
            int controlIndex, testIndex;
            for (controlIndex = testIndex = 0;
                 controlIndex < controlLen && testIndex < testLen;
                 ) {
                // find next non-text child nodes
                XmlNode c = controlChildren[controlIndex];
                while (IsText(c) && ++controlIndex < controlLen) {
                    c = controlChildren[controlIndex];
                }
                if (IsText(c)) {
                    break;
                }
                XmlNode t = testChildren[testIndex];
                while (IsText(t) && ++testIndex < testLen) {
                    t = testChildren[testIndex];
                }
                if (IsText(t)) {
                    break;
                }

                // different types of children make elements
                // non-comparable
                if (c.NodeType != t.NodeType) {
                    return false;
                }
                // recurse for child elements
                if (c is XmlElement
                    && !ByNameAndTextRec(c as XmlElement, t as XmlElement)) {
                    return false;
                }

                controlIndex++;
                testIndex++;
            }

            // child lists exhausted?
            if (controlIndex < controlLen) {
                XmlNode n = controlChildren[controlIndex];
                while (IsText(n) && ++controlIndex < controlLen) {
                    n = controlChildren[controlIndex];
                }
                // some non-Text children remained
                if (controlIndex < controlLen) {
                    return false;
                }
            }
            if (testIndex < testLen) {
                XmlNode n = testChildren[testIndex];
                while (IsText(n) && ++testIndex < testLen) {
                    n = testChildren[testIndex];
                }
                // some non-Text children remained
                if (testIndex < testLen) {
                    return false;
                }
            }
            return true;
        }

        private static bool BothNullOrEqual(object o1, object o2) {
            return o1 == null ? o2 == null : o1.Equals(o2);
        }

        private static bool
            MapsEqualForKeys(IDictionary<XmlQualifiedName, string> control,
                             IDictionary<XmlQualifiedName, string> test,
                             IEnumerable<XmlQualifiedName> keys) {
            return !keys.Any(q => {
                string c, t;
                return control.TryGetValue(q, out c) != test.TryGetValue(q, out t)
                    || !BothNullOrEqual(c, t);
            });
        }

        private static bool IsText(XmlNode n) {
            return n is XmlText || n is XmlCDataSection;
        }
    }
}