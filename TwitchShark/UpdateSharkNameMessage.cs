using System;

[Serializable]
class UpdateSharkNameMessage : Message
{
    public uint sharkId;
    public String name;
    public String color;

    public UpdateSharkNameMessage(Messages type, uint sharkId, String name, String color) : base(type)
    {
        this.sharkId = sharkId;
        this.name = name;
        this.color = color;
    }
}
