using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace ProductService.Serializers;

public class ObjectIdToStringSerializer : SerializerBase<string>
{
    public override string Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonType = context.Reader.GetCurrentBsonType();

        if (bsonType == BsonType.ObjectId)
        {
            var objectId = context.Reader.ReadObjectId();
            return objectId.ToString();
        }
        else if (bsonType == BsonType.String)
        {
            return context.Reader.ReadString();
        }
        else if (bsonType == BsonType.Null)
        {
            context.Reader.ReadNull();
            return string.Empty;
        }

        throw new NotSupportedException($"Cannot deserialize BsonType {bsonType} to string");
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            context.Writer.WriteNull();
        }
        else if (ObjectId.TryParse(value, out var objectId))
        {
            context.Writer.WriteObjectId(objectId);
        }
        else
        {
            context.Writer.WriteString(value);
        }
    }
}
