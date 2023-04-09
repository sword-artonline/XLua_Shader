func1 = function()
print("this is func1")
end

func2 = function(num)
print("num is :"..num)
end

func3 = function()
    return "thi is func3"
end

func4 = function()
    return "this is func4:" , 1
end

testTable={}

testTable.func4 = fun4

testTable.func1 = func1

testTable.name = "admin"

testTable.hp = 100

testTable.func2 = func2

function testTable:func5()
    print("this is func5" .. self.name)
end