import os
import sys
import re

CACHE_FILE = "cached_translations"

KEYS_TO_TRANSLATE = [
    "achievementName",
    "achievementDescription",
    "text",
]

def log(text):
    sys.stdout.write(text + "\n")
    sys.stdout.flush()

class JankyTranslator:

    RX_KEY_VAL = re.compile("^(\s*)\"([^\"]+)\": \"([^\"]+)\"(.*)$")

    def __init__(self, template_dir, language, out_dir):
        self.template_dir = template_dir
        self.language = language
        self.out_dir = out_dir
        
    def read_file(self, path):
        f = open(path, "r")
        data = f.read()
        f.close()
        return data
    
    def write_file(self, path, data):
        f = open(path, "w")
        f.write(data)
        f.close()

    def load_cached_translations(self):
        if (not os.path.exists(CACHE_FILE)):
            self.cached_translations = {}
            return
        self.cached_translations = eval(self.read_file(CACHE_FILE))
        
    def write_cached_translations(self):
        self.write_file(CACHE_FILE, str(self.cached_translations))

    def translate_file(self, path):
        log("--> in: " + path)
        data = self.read_file(path)
        out_path = os.path.join(self.out_dir, os.path.basename(path).replace("English_", self.language + "_"))
        log("--> out: " + out_path)
        fixed_lines = []
        for line in data.split("\n"):
            match = self.RX_KEY_VAL.match(line)
            if (match is None):
                fixed_lines = line
                continue
            prefix, key, val, suffix = match.groups()
            if (key not in KEYS_TO_TRANSLATE):
                fixed_lines = line
                continue
            #log("%s = %s" % (key, val))
        return 0
    
    def run(self):
        log("Running JankyTranslator magic (template_dir: '%(template_dir)s', language: '%(language)s', out_dir: '%(out_dir)s'))." % self.__dict__)
        self.load_cached_translations()
        for file in os.listdir(self.template_dir):
            full_path = os.path.join(self.template_dir, file)
            if (os.path.exists(full_path) and os.path.isfile(full_path)):
                self.translate_file(full_path)
        self.write_cached_translations()
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