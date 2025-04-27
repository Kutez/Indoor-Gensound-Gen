from pedalboard import Pedalboard, Gain, Reverb, LowShelfFilter
from pedalboard.io import AudioFile
from pathlib import Path
import os
import sys
import json

sndType = ["shoot","shoot_actor","silncer_shoot","silncer_shot_actor"]
extraSndList = ["bolt","pump"]

print("Commands :")
print("[none or anything else] - Add more sounds, wont delete anything")
print("delete - Delete all sounds, uninstall the mod")
print("reset - Delete and then regenerate sounds, do this when you change the settings")

config = json.load(open("indoor_config.json","r"))

cmd = input("Type command: ")

file_path = Path(__file__).resolve().parents[0].__str__()

if getattr(sys, "frozen", False) and hasattr(sys, "_MEIPASS"):
    file_path = Path(sys.executable).resolve().parents[0].__str__()

def getFileName(stri):
    return stri.split("\\")[len(stri.split("\\"))-1]

def genSound(input,name):
    board = Pedalboard([
        Gain(1.5),
        LowShelfFilter(cutoff_frequency_hz = 15000, gain_db = config["loudness_Gain"]),
        Reverb(room_size= config["room_size"], 
        damping= config["damping"], 
        wet_level = config["wet_level"], 
        dry_level = config["dry_level"], 
        width = config["width"], 
        freeze_mode = config["freeze_mode"]),
])
    for i in bRange(extraSndList):
        if name.find(extraSndList[i])!=-1:
                board = Pedalboard([
                    Gain(1.25),
                    LowShelfFilter(cutoff_frequency_hz = 15000, gain_db = config["loudness_Gain"]*0.55),
                    Reverb(room_size= config["room_size"], 
                    damping= config["damping"], 
                    wet_level = config["wet_level"]*1.25, 
                    dry_level = config["dry_level"], 
                    width = config["width"], 
                    freeze_mode = config["freeze_mode"]),])
                break

    with AudioFile(input.__str__().replace("\n","")) as f:
    # Open an audio file to write to:
        with AudioFile(input.resolve().parents[0].__str__()+"\\"+name, "w", f.samplerate, f.num_channels) as o:
        # Read one second of audio at a time, until the file is empty:
            while f.tell() < f.frames:
                chunk = f.read(f.samplerate)
                effected = board(chunk, f.samplerate, reset=False)
                o.write(effected)
output = open("output.txt","r").readlines()
s = 1
open(file_path+"\\gamedata\\configs\\mod_system_aaaaa_indoor_bak_gen.ltx","w").close() # Create a new config
config_file = open(file_path+"\\gamedata\\configs\\mod_system_aaaaa_indoor_bak_gen.ltx","a")

def getSndName(i):
    return sndType[i]

def getFullSndName(i):
    return "snd_"+sndType[i]

def getLayersCount(dirs,sound_type):
    if sound_type == "silncer_shoot": # it's my fault :)
        sound_type = "silncer_shot"
    for i in range(len(dirs)):
        if dirs[i].find("snd_"+sound_type)!=-1:
            return int(dirs[i][len(dirs[i])-1]),i+1

        
def createBlankArray(type):
    a = []
    for i in range(len(sndType)):
        if type == "num":
            a.append(0)
        if type == "str":
            a.append("")
        if type == "array":
            a.append([])
    return a

def getExtraName(dir):
    for i in range(len(extraSndList)):
        if getFileName(dir).find(extraSndList[i])!=-1:
            return "_"+extraSndList[i]
    return ""

def bRange(ar,m = -1):
    if type(ar) is list:
        i = 0
        a = []
        while i < len(ar):
            a.append(i)
            i += 1
        return a
    if type(ar) is int:
        i = ar
        a = []
        while i < m:
            a.append(i)
            i += 1
        return a

if cmd == "delete" or cmd == "reset":
    remove_list = []
    for root, dir, files in os.walk(file_path+"\\gamedata\\sounds"):
        for f in files:
            if f.find("indoor_gunsnd_bak")!=-1:
                remove_list.append(root + "\\" + f)
    if len(remove_list) > 0:
        print("Old Sounds Detected, Removing")
        for i in range(len(remove_list)):
            print("Remove ", i, " Files")
            os.remove(remove_list[i])

if cmd != "delete":
    for m in range(len(output)):
        dirs = output[m].split("..")
        dirs[len(dirs)-1] = dirs[len(dirs)-1].replace("\n","") # clean up
        wp_name = dirs[0]
        sndDirs = createBlankArray("array")
        sndFiles = createBlankArray("array")
        sndLayerStart = createBlankArray("num")
        sndLayerCount = createBlankArray("num")
        for i in range(len(sndType)):
            sndLayerCount[i],sndLayerStart[i] = getLayersCount(dirs,getSndName(i))
            for k in bRange(sndLayerStart[i],sndLayerStart[i]+sndLayerCount[i]):
                sndDirs[i].append(dirs[k])
                sndFiles[i].append(Path(file_path+"\\gamedata\\sounds\\"+dirs[k]+".ogg"))
        if sndFiles[0] and sndFiles[0][0].is_file():
            print("File found.")
            config_file.write(";-------"+wp_name+"---------"+"\n")
            config_file.write("!["+wp_name+"]"+"\n")
            #######
            sndProcessedFiles = createBlankArray("array")
            for i in range(len(sndType)):
                if sndDirs[i]:
                    for j in bRange(sndDirs[i]):
                        if sndFiles[i][j].is_file():
                            extraName = getExtraName(sndDirs[i][j])
                            fileName = "indoor_gunsnd_bak_"+getFullSndName(i)+extraName+str(j)+".ogg"
                            if not Path(sndFiles[i][j].resolve().parents[0].__str__()+"\\"+fileName).is_file():
                                genSound(sndFiles[i][j],fileName)
                            sndProcessedFiles[i].append(Path(sndDirs[i][j]).resolve().parents[0].__str__()+"\\"+fileName.replace(".ogg",""))
                    config_file.write(getFullSndName(i)+"_indoor = "+wp_name+"_"+getFullSndName(i)+"_bak\n")
            for i in range(len(sndType)):
                if sndDirs[i] and sndProcessedFiles[i]:
                    config_file.write("["+wp_name+"_"+getFullSndName(i)+"_bak]"+"\n")
                    extraNameLayer = {}
                    extraLayersCount = 2
                    for j in bRange(sndProcessedFiles[i]):
                        extraed = False
                        for extra in extraSndList:
                            if getFileName(sndProcessedFiles[i][j]).find(extra)!=-1:
                                if extra not in extraNameLayer:
                                    extraNameLayer[extra] = [extraLayersCount,0]
                                    extraLayersCount+=1
                                index = extraNameLayer[extra][1]
                                if index == 0: 
                                    index = "" 
                                config_file.write("snd_"+str(extraNameLayer[extra][0])+"_layer"+str(index)+" = "+sndProcessedFiles[i][j].__str__().replace(file_path+"\\","")+"\n")
                                extraed = True
                                extraNameLayer[extra][1] +=1
                        if not extraed:
                            num = j
                            if j==0: 
                                num = ""
                            config_file.write("snd_1_layer"+str(num)+" = "+sndProcessedFiles[i][j].__str__().replace(file_path+"\\","")+"\n")
                    config_file.write("\n")
        print("Processing: ",s,"/",len(output))
        s+=1
config_file.close()