
#--------------------------------------------------------------------------
# -- DISCLAIMER --
#
# This script is used at end-user's own risk.  The author is not
# responsible for any charges incurred with Google for API usage.
#
# This script uses the Google Translate API. If you use this script then
# you must provide your own project and credentials.  It is your
# responsibility to set limits/quotas on the number of characters
# translated by calls to the API.  The script will attempt to translate
# all English phrases in the text files (see KEYS_TO_TRANSLATE to see
# which specific portions of the files will be translated).  While the
# script does use caching to limit API calls with multiple runs, it 
# *does not* do any limiting on characters translated.  As such, it
# is absolutely 100% your responsibility to monitor and limit activity.
#
#--------------------------------------------------------------------------

import os
import sys
import re
from google.cloud import translate, translate_v2

# To set up default creds for google api (run in Google Cloud SDK shell):
# gcloud auth application-default login

THIS_DIR = os.path.dirname(__file__)
TMP_DIR = os.path.join(THIS_DIR, "tmp")
PROJECT_ID_FILE = os.path.join(TMP_DIR, "project_id")
CACHE_FILE = os.path.join(TMP_DIR, "cache")

KEYS_TO_TRANSLATE = [
    "achievementDescription",
    "achievementName",
    "animalPlaceholderName",
    "animalSpeciesDescription",
    "animalSpeciesName",
    "birthdayAnnouncement",
    "optionText",
    "response",
    "text",
    "unlockBody",
    "unlockHeader",
    "unlockSubTitle",
    "unlockTitle",
]

LISTS_TO_TRANSLATE = [
    "observations",
    "relationshipStatus",
    "titles",
]

def log(text, new_line = True):
    sys.stdout.write(text + ("\n" if (new_line) else ""))
    sys.stdout.flush()

class JankyTranslator:

    RX_KEY_VAL = re.compile(r"^([ \t]*\")([^\"]+)(\": \")([^\"]+)(\".*)$")
    RX_LIST_START = re.compile(r"^[ \t]*\"([^\"]+)\": \[[ \t]*$")
    RX_LIST_ITEM = re.compile(r"^([ \t]*\")([^\"]+)(\".*)$")

    def __init__(self, template_dir, language, out_dir):
        self.template_dir = template_dir
        self.language = language
        self.language_code = None
        self.out_dir = os.path.join(out_dir, self.language)
        os.makedirs(self.out_dir, exist_ok = True)
        self.project_id = self.read_file(PROJECT_ID_FILE).strip()
        self.translate_parent = "projects/%(project_id)s/locations/global" % self.__dict__
        
    def read_file(self, path):
        f = open(path, "r")
        data = f.read()
        f.close()
        return data
    
    def write_file(self, path, data):
        f = open(path, "w")
        f.write(data)
        f.close()

    def load_cache(self):
        if (os.path.exists(CACHE_FILE)):
            self.cache = eval(self.read_file(CACHE_FILE))
        for key in ('languages', 'translations', 'stats'):
            if (key not in self.cache.keys()):
                self.cache[key] = {}
        
    def write_cache(self):
        self.write_file(CACHE_FILE, str(self.cache))

    def set_language_code(self):
        
        def __set_language_code__(do_raise):
            self.language_code = self.cache['languages'].get(self.language, None)
            if (self.language_code is None):
                if (do_raise):
                    for key, val in self.cache['languages'].items():
                        log("%s: %s" % (key, val))
                    log("** set_language_code ERROR - unknown language '%(language)s'; see list above." % self.__dict__)
                    sys.exit(1)
                return False
            log("Language Code: " + self.language_code)
            return True
        
        if (__set_language_code__(False)):
            return
        log("Unable to retrieve language code for %(language)s from cache; fetching codes from Google API." % self.__dict__)
        for language in translate_v2.Client().get_languages():
            self.cache['languages'][language['name']] = language['language']
        __set_language_code__(True)

    def translate_string(self, text):
        if (not self.language_code in self.cache['translations'].keys()):
            self.cache['translations'][self.language_code] = {}
        result = self.cache['translations'][self.language_code].get(text, None)
        if (result is not None):
            return result
        if ('total_chars_translated' not in self.cache['stats'].keys()):
            self.cache['stats']['total_chars_translated'] = 0
        self.cache['translations'][self.language_code][text] = translate.TranslationServiceClient().translate_text(
            request = {
                "parent": self.translate_parent,
                "contents": [text,],
                "mime_type": "text/plain",
                "source_language_code": "en-US",
                "target_language_code": self.language_code,
            }
        ).translations[0].translated_text
        self.cache['stats']['total_chars_translated'] += len(text)
        return self.cache['translations'][self.language_code][text]

    def translate_marked_up_string(self, text):
        chunks = []
        index = 0
        chunk = ""
        in_tag = False
        while (index < len(text)):
            if (in_tag and text[index] == '>'):
                chunks.append(chunk + ">")
                chunk = ""
                in_tag = False
            elif (text[index] == '<'):
                chunks.append(chunk)
                chunk = "<"
                in_tag = True
            else:
                chunk += text[index]
            index += 1
        if (chunk != ""):
            chunks.append(chunk)
        new_chunks = []
        for chunk in chunks:
            chunk = chunk.strip()
            if (chunk != ""):
                new_chunks.append(chunk if (chunk[0] == '<') else self.translate_string(chunk))
        return "".join(new_chunks)
        
    def translate_file(self, path):
        log("--> in: " + path)
        data = self.read_file(path)
        out_path = os.path.join(self.out_dir, os.path.basename(path).replace("English_", self.language + "_"))
        log("--> out: " + out_path)
        fixed_lines = []
        lines = data.split("\n")
        self.in_list = False
        for line in lines:

            def add_key_val():
                if (self.in_list):
                    return False
                match = self.RX_KEY_VAL.match(line)
                if (match is None):
                    return False
                prefix, key, middle, val, suffix = match.groups()
                if (key not in KEYS_TO_TRANSLATE):
                    return False
                fixed_lines.append("".join((prefix, key, middle, self.translate_marked_up_string(val), suffix)))
                return True
            
            def add_list_val():
                if (not self.in_list):
                    return False
                if (line.strip().startswith("]")):
                    self.in_list = False
                    return False
                match = self.RX_LIST_ITEM.match(line)
                if (match is None):
                    return False
                prefix, val, suffix = match.groups()
                fixed_lines.append("".join((prefix, self.translate_marked_up_string(val), suffix)))
                return True
            
            def add_everything_else():
                match = self.RX_LIST_START.match(line)
                if (match is not None):
                    self.in_list = match.group(1) in LISTS_TO_TRANSLATE
                fixed_lines.append(line)
                return True

            log("--> Progress: %d / %d lines      \r" % (len(fixed_lines), len(lines)), new_line = False)
            for func in (add_key_val, add_list_val, add_everything_else):
                if (func()):
                    break
        self.write_file(out_path, "\n".join(fixed_lines))
        return 0
    
    def run(self):
        log("Running JankyTranslator magic (template_dir: '%(template_dir)s', language: '%(language)s', out_dir: '%(out_dir)s'))." % self.__dict__)
        self.load_cache()
        self.set_language_code()
        try:
            for file in os.listdir(self.template_dir):
                full_path = os.path.join(self.template_dir, file)
                if (os.path.exists(full_path) and os.path.isfile(full_path)):
                    self.translate_file(full_path)
        except KeyboardInterrupt as e:
            log("\n* Keyboard interrupt")
        finally:
            log("Accumulated Total Chars Translated: %dK" % (self.cache['stats']['total_chars_translated'] / 1000))
            self.write_cache()
        return 0

def main(argv):
    argc = len(argv)
    if (argc < 4):
        log("usage: %s <template-dir> <language> <out-dir>" % argv[0])
        return 1
    return JankyTranslator(argv[1], argv[2], argv[3]).run()

if (__name__ == "__main__"):
    #sys.exit(main(sys.argv))
    sys.exit(main([
        "", "tmp/template", "Spanish", "tmp/output"
    ]))