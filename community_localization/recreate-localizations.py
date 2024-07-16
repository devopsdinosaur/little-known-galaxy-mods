# =========================================================================
# Copyright (c) 2024 devopsdinosaur (devopsdinosaur@gmail.com)
#
# This file may not be modified, copied, redistributed, or used for 
# monetary gain without the express written consent of the author.
# =========================================================================
#
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
#
# ** Setup **
#
# 1. Create an account and project on Google Cloud that has Cloud Translation
# API enabled.  Create a 'tmp' directory here and 'project_id' file
# within it that contains the project ID of your project.
# 2. Install latest Python 3 version (https://www.python.org/downloads/).
# 3. Install gcloud CLI (https://cloud.google.com/sdk/docs/install).
# 4. In VSCode console:
# $ pip install google-cloud-translate
# 5. In Google Cloud SDK shell:
# $ gcloud auth application-default login
# $ gcloud auth application-default set-quota-project <project-id>
# 6. Using AssetStudio, extract all the 'English_XXX' TextAssets.  This directory
# will be used as your <template-dir> in the script call below.
# 7. This script should be good to run as follows:
# $ python3 ./recreate-localizations
#
# To add a langauge for translation, uncomment the "Gibberish" line in the
# LANGUAGES list below and run the script.  The script will not recognize
# the language (obviously =) and will print out a list of valid language
# names.  Add the language exactly as it's printed in the output to the
# LANGUAGES list and re-comment the "Gibberish" line.  Run the script 
# again, and it should start translating.

LANGUAGES = (
    # "Gibberish",

    "German",
    "Spanish",
    "Portuguese",
    "French",
    "Korean",                   # character set not yet fully supported?
    #"Chinese (Simplified)",     # character set not yet fully supported?
    #"Italian",
    #"Japanese",                 # character set not yet fully supported?
)

import os
import sys
import re
import time
from google.cloud import translate, translate_v2
import pickle
import traceback

THIS_DIR = os.path.dirname(__file__)
TMP_DIR = os.path.join(THIS_DIR, "tmp")
PROJECT_ID_FILE = os.path.join(TMP_DIR, "project_id")
CACHE_FILE = os.path.join(TMP_DIR, "cache")
CACHE_FILE_BACKUP = os.path.join(TMP_DIR, "cache_backup")
TEMPLATE_DIR = os.path.join(THIS_DIR, "template")
OUT_DIR = os.path.join(THIS_DIR, "localizations")

INVALID_CHARS = "ÃÂ"

STRING_REPLACEMENTS = [
    ('"', "'"),
    ("\\ n", "\\n"),
    ("\\N", "\\n"),
]

KEYS_TO_TRANSLATE = [
    "achievementDescription",
    "achievementName",
    "animalPlaceholderName",
    "animalSpeciesDescription",
    "animalSpeciesName",
    "birthdayAnnouncement",
    "collectionName",
    "emailBody",
    "emailSubject",
    "eventDescription",
    "eventName",
    "interfaceText",
    "itemDescription",
    "itemName",
    "libraryBody",
    "librarySubtopic",
    "locationCalendar",
    "locationCalendarUnknown",
    "locationDescription",
    "locationDescriptUnknown",
    "locationName",
    "locationNameUnknown",
    "locationTooltip",
    "optionText",
    "questDescription",
    "questName",
    "response",
    "text",
    "unlockBody",
    "unlockHeader",
    "unlockSubTitle",
    "unlockTitle",
]

LISTS_TO_TRANSLATE = [
    "goalDescriptions",
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
    CACHE_WRITE_FREQUENCY = 30.0

    def __init__(self, template_dir, language, out_dir):
        self.template_dir = template_dir
        self.language = language
        self.language_code = None
        self.out_dir = os.path.join(out_dir, self.language)
        self.project_id = self.read_file(PROJECT_ID_FILE).strip()
        self.translate_parent = "projects/%(project_id)s/locations/global" % self.__dict__
        self.last_cache_write_time = None
        
    def read_file(self, path):
        f = open(path, "r")
        data = f.read()
        f.close()
        return data
    
    def write_file(self, path, data):
        os.makedirs(os.path.dirname(path), exist_ok = True)
        f = open(path, "w")
        try:
            f.write(data)
        except UnicodeEncodeError as e:
            # There is definitely a better way to do this, but shouldn't
            # hit here often and file writes are not frequent.
            new_data = ""
            for index in range(len(data)):
                try:
                    new_data += chr(int(data[index].encode("iso-8859-1")[0]))
                except UnicodeEncodeError as e:
                    pass
            f.write(new_data)
        f.close()

    def load_cache(self):
        self.cache = {}
        try:
            f = open(CACHE_FILE, "rb")
            self.cache = pickle.load(f)
            f.close()
        except Exception as e:
            traceback.print_exc()
            try:
                f = open(CACHE_FILE, "rb")
                self.cache = pickle.load(f)
                f.close()
                log("* warning - unable to load primary cache; restoring from backup.")
            except:
                pass
        for key in ('languages', 'translations', 'stats'):
            if (key not in self.cache.keys()):
                self.cache[key] = {}
        
    def write_cache(self):
        os.makedirs(os.path.dirname(CACHE_FILE), exist_ok = True)
        f = open(CACHE_FILE, "wb")
        pickle.dump(self.cache, f)
        f.close()

    def write_cache_backup(self):
        os.makedirs(os.path.dirname(CACHE_FILE_BACKUP), exist_ok = True)
        f = open(CACHE_FILE_BACKUP, "wb")
        pickle.dump(self.cache, f)
        f.close()

    def write_cache_periodic(self):
        if (self.last_cache_write_time is not None and time.time() - self.last_cache_write_time < self.CACHE_WRITE_FREQUENCY):
            return
        self.write_cache_backup()
        self.write_cache()
        self.last_cache_write_time = time.time()

    def set_language_code(self):
        
        def __set_language_code__(do_raise):
            self.language_code = self.cache['languages'].get(self.language, None)
            if (self.language_code is None):
                if (do_raise):
                    for key, val in self.cache['languages'].items():
                        log(key)
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

    def contains_invalid_chars(self, text):
        for c in text:
            for c2 in INVALID_CHARS:
                if (c2 == c):
                    return True
        return False

    def translate_string(self, text, use_cache = True):
        if (not self.language_code in self.cache['translations'].keys()):
            self.cache['translations'][self.language_code] = {}
        result = self.cache['translations'][self.language_code].get(text, None)
        if (use_cache and result is not None and not self.contains_invalid_chars(result)):
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

    def apply_string_replacements(self, text):
        for old, new in STRING_REPLACEMENTS:
            text = text.replace(old, new)
        return text
    
    def translate_marked_up_string(self, text):
    
        def get_next_chunk(index, text):
            
            def is_stop_tag(c):
                return c == '>'
            
            def is_stop_variable(c):
                return not (c.isalpha() or c.isdigit())
            
            def is_stop_basic(c):
                return c in "<$"
        
            if (index >= len(text)):
                return (index, None)
            stop_check = is_stop_tag if (text[index] == '<') else is_stop_variable if (text[index] == '$') else is_stop_basic
            chunk = ""
            while (True):
                chunk += text[index]
                index += 1
                if (index >= len(text) or stop_check(text[index])):
                    if (index < len(text) - 1 and text[index] == '>'):
                        return (index + 1, chunk + '>')
                    break
            return (index, chunk)

        index = 0
        chunks = []
        while (True):
            index, chunk = get_next_chunk(index, text)
            if (not chunk):
                break
            chunks.append(chunk if (chunk[0] in "<$") else self.translate_string(chunk))
        return self.apply_string_replacements("".join(chunks))
 
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
            self.write_cache_periodic()
        self.write_file(out_path, "\n".join(fixed_lines))
        return 0
    
    def print_word_count(self):
        word_count = 0
        for chunks in self.cache['translations'].values():
            for chunk in chunks:
                word_count += len(chunk.split())
            break
        log("\nWord Count: %dK" % (word_count / 1000))

    def run(self):
        log("Running JankyTranslator magic (template_dir: '%(template_dir)s', language: '%(language)s', out_dir: '%(out_dir)s'))." % self.__dict__)
        self.load_cache()
        #print(str(self.cache))
        #sys.exit(0)
        self.set_language_code()
        try:
            for file in os.listdir(self.template_dir):
                full_path = os.path.join(self.template_dir, file)
                if (os.path.exists(full_path) and os.path.isfile(full_path)):
                    self.translate_file(full_path)
        except KeyboardInterrupt as e:
            log("\n* Keyboard interrupt")
            sys.exit(1)
        finally:
            self.print_word_count()
            log("Accumulated Total Chars Translated: %dK" % (self.cache['stats']['total_chars_translated'] / 1000))
            self.write_cache()
        return 0

def main(argv):
    for language in LANGUAGES:
        JankyTranslator(TEMPLATE_DIR, language, OUT_DIR).run()
    return 0
    #argc = len(argv)
    #if (argc < 4):
    #    log("usage: %s <template-dir> <language> <out-dir>" % argv[0])
    #    return 1
    #return JankyTranslator(argv[1], argv[2], argv[3]).run()

if (__name__ == "__main__"):
    sys.exit(main(sys.argv))
    #sys.exit(main([
    #    "", 
    #    "tmp/template", 
    #    "French", 
    #    "tmp/output"
    #]))