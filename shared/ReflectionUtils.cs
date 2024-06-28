using System;
using System.Collections.Generic;
using System.Reflection;

public static class ReflectionUtils {

    public const BindingFlags BINDING_FLAGS_ALL = (BindingFlags) 65535;

    public static string list_members(object obj) {
        List<string> lines = new List<string>();
        if (obj == null) {
            return "object is null";
        }
        lines.Add($"list_members (object type: {obj.GetType().Name})");
        foreach (FieldInfo field in obj.GetType().GetFields(BINDING_FLAGS_ALL)) {
            lines.Add($"--> field: {field.Name} ({field.FieldType.Name})");
        }
        foreach (MethodInfo method in obj.GetType().GetMethods(BINDING_FLAGS_ALL)) {
            lines.Add($"--> method: {method.Name}");
        }
        foreach (PropertyInfo property in obj.GetType().GetProperties(BINDING_FLAGS_ALL)) {
            lines.Add($"--> property: {property.Name}");
        }
        return string.Join("\n", lines);
    }

    public static FieldInfo get_field(object obj, string name) {
        return obj.GetType().GetField(name, BINDING_FLAGS_ALL);
    }

    public static object get_field_value(object obj, string name) {
        return get_field(obj, name)?.GetValue(obj);
    }

    public static PropertyInfo get_property(object obj, string name) {
        return obj.GetType().GetProperty(name, BINDING_FLAGS_ALL);
    }

    public static object get_property_value(object obj, string name) {
        return get_property(obj, name)?.GetValue(obj);
    }

    public static MethodInfo get_method(object obj, string name) {
        return obj.GetType().GetMethod(name, BINDING_FLAGS_ALL);
    }

    public static object invoke_method(object obj, string name, object[] _params = null) {
        return get_method(obj, name)?.Invoke(obj, (_params == null ? new object[] { } : _params));
    }

    public class EnumerateListEntriesCallbackParams {
        private object list;
        private int index;
        public object Index {
            get {
                return this.index;
            }
        }
        public object Value {
            get {
                return this.get_Item?.Invoke(this.list, new object[] { this.index });
            }
            set {
                this.set_Item?.Invoke(this.list, new object[] { this.index, value });
            }
        }
        private MethodInfo get_Item;
        private MethodInfo set_Item;

        public EnumerateListEntriesCallbackParams(object list, int index, MethodInfo get_Item, MethodInfo set_Item) {
            this.list = list;
            this.index = index;
            this.get_Item = get_Item;
            this.set_Item = set_Item;
        }
    }

    public static void enumerate_list_entries(object list, Func<EnumerateListEntriesCallbackParams, bool> callback) {
        MethodInfo get_Item = get_method(list, "get_Item");
        MethodInfo set_Item = get_method(list, "set_Item");
        for (int index = 0; index < (int) get_field_value(list, "_size"); index++) {
            EnumerateListEntriesCallbackParams callback_params = new EnumerateListEntriesCallbackParams(list, index, get_Item, set_Item);
            if (!callback.Invoke(callback_params)) {
                break;
            }
        }
    }

    public class EnumerateDictEntriesCallbackParams {
        private object dict;
        private object key;
        public object Key {
            get {
                return this.key;
            }
        }
        public object Value {
            get {
                return this.get_Item?.Invoke(this.dict, new object[] { this.Key });
            }
            set {
                this.set_Item?.Invoke(this.dict, new object[] { this.Key, value });
            }
        }
        private MethodInfo get_Item;
        private MethodInfo set_Item;

        public EnumerateDictEntriesCallbackParams(object dict, object key, MethodInfo get_Item, MethodInfo set_Item) {
            this.dict = dict;
            this.key = key;
            this.get_Item = get_Item;
            this.set_Item = set_Item;
        }
    }

    public static void enumerate_dict_entries(object dict, Func<EnumerateDictEntriesCallbackParams, bool> callback) {
        MethodInfo get_Item = get_method(dict, "get_Item");
        MethodInfo set_Item = get_method(dict, "set_Item");
        object entries = get_field_value(dict, "entries");
        if (entries == null) {
            entries = get_field_value(dict, "_entries");
            if (entries == null) {
                return;
            }
        }
        foreach (object entry in (Array) entries) {
            EnumerateDictEntriesCallbackParams callback_params = new EnumerateDictEntriesCallbackParams(dict, get_field_value(entry, "key"), get_Item, set_Item);
            if (callback_params.Key == null) {
                continue;
            }
            if (!callback.Invoke(callback_params)) {
                break;
            }
        }
    }
}